package org.eclipse.jdt.internal.core.newbuilder;
/*
 * (c) Copyright IBM Corp. 2000, 2001.
 * All Rights Reserved.
 */
import org.eclipse.core.resources.*;
import org.eclipse.core.runtime.*;
import org.eclipse.jdt.core.*;
import org.eclipse.jdt.internal.compiler.util.CharOperation;
import org.eclipse.jdt.internal.core.Util;
import java.io.*;
import java.util.*;
public class State {
String javaProjectName;
ClasspathLocation[] classpathLocations;
String outputLocationString;
SimpleLookupTable references;
SimpleLookupTable typeLocations;
int buildNumber;
int lastStructuralBuildNumber;
SimpleLookupTable structuralBuildNumbers;
static final byte VERSION = 0x0003;
State() { }
protected State(JavaBuilder javaBuilder) {
       this.javaProjectName = javaBuilder.currentProject.getName();
       this.classpathLocations = javaBuilder.classpath;
       this.outputLocationString = javaBuilder.outputFolder.getLocation().toString();
       this.references = new SimpleLookupTable(13);
       this.typeLocations = new SimpleLookupTable(13);
       this.buildNumber = 0; // indicates a full build
       this.lastStructuralBuildNumber = this.buildNumber;
       this.structuralBuildNumbers = new SimpleLookupTable(3); }
void copyFrom(State lastState) {
       try {
             this.references = (SimpleLookupTable) lastState.references.clone();
             this.typeLocations = (SimpleLookupTable) lastState.typeLocations.clone();
             this.buildNumber = lastState.buildNumber + 1;
             this.lastStructuralBuildNumber = lastState.lastStructuralBuildNumber;
       } catch (CloneNotSupportedException e) {
             this.references = new SimpleLookupTable(31);
             Object[] keyTable = lastState.references.keyTable;
             Object[] valueTable = lastState.references.valueTable;
             for (int i = 0, l = keyTable.length; i < l; i++)
                  if (keyTable[i] != null)
                      this.references.put(keyTable[i], valueTable[i]);
             this.typeLocations = new SimpleLookupTable(31);
             keyTable = lastState.typeLocations.keyTable;
             valueTable = lastState.typeLocations.valueTable;
             for (int i = 0, l = keyTable.length; i < l; i++)
                  if (keyTable[i] != null)
                      this.typeLocations.put(keyTable[i], valueTable[i]); } }
char[][] getDefinedTypeNamesFor(String location) {
       Object c = references.get(location);
       if (c instanceof AdditionalTypeCollection)
             return ((AdditionalTypeCollection) c).definedTypeNames;
       return null;  }// means only one type is defined with the same name as the file... saves space
void hasStructuralChanges() {
       this.lastStructuralBuildNumber = this.buildNumber; }
boolean isDuplicateLocation(String qualifiedName, String location) {
       String existingLocation = (String) typeLocations.get(qualifiedName);
       return existingLocation != null && !existingLocation.equals(location); }
boolean isStructurallyChanged(IProject prereqProject, State prereqState) {
       Object o = structuralBuildNumbers.get(prereqProject.getName());
       if (prereqState != null) {
             int previous = o == null ? 0 : ((Integer) o).intValue();
             if (previous == prereqState.lastStructuralBuildNumber && prereqState.buildNumber > 0) return false; }
       return true; }
void locationForType(String qualifiedName, String location) {
       typeLocations.put(qualifiedName, location); }
void record(String location, char[][][] qualifiedRefs, char[][] simpleRefs, char[] mainTypeName, ArrayList typeNames) {
       if (typeNames.size() == 1 && CharOperation.equals(mainTypeName, (char[]) typeNames.get(0))) {
                  references.put(location, new ReferenceCollection(qualifiedRefs, simpleRefs));
       } else {
             char[][] definedTypeNames = new char[typeNames.size()][]; // can be empty when no types are defined
             typeNames.toArray(definedTypeNames);
             references.put(location, new AdditionalTypeCollection(definedTypeNames, qualifiedRefs, simpleRefs)); } }
void recordLastStructuralChanges(IProject prereqProject, int prereqBuildNumber) {
       structuralBuildNumbers.put(prereqProject.getName(), new Integer(prereqBuildNumber)); }
void remove(IPath filePath) {
       String locationToRemove = filePath.toString();
       references.removeKey(locationToRemove);
       typeLocations.removeValue(locationToRemove); }
void removePackage(IResourceDelta sourceDelta) {
       IResource resource = sourceDelta.getResource();
       switch(resource.getType()) {
             case IResource.FOLDER :
                  IResourceDelta[] children = sourceDelta.getAffectedChildren();
                  for (int i = 0, length = children.length; i < length; ++i)
                      removePackage(children[i]);
                  return;
             case IResource.FILE :
                  IPath location = resource.getLocation();
                  if (JavaBuilder.JAVA_EXTENSION.equalsIgnoreCase(location.getFileExtension()))
                      remove(location); } }
static State read(DataInputStream in) throws IOException {
       if (JavaBuilder.DEBUG)
             System.out.println("About to read state..."); //$NON-NLS-1$
       if (VERSION != in.readByte())
             throw new IOException(Util.bind("build.unhandledVersionFormat")); //$NON-NLS-1$
       State newState = new State();
       newState.javaProjectName = in.readUTF();
       newState.buildNumber = in.readInt();
       newState.lastStructuralBuildNumber = in.readInt();
       newState.outputLocationString = in.readUTF();
       int length = in.readInt();
       newState.classpathLocations = new ClasspathLocation[length];
       for (int i = 0; i < length; ++i) {
             switch (in.readByte()) {
                  case 1 :
                      newState.classpathLocations[i] = ClasspathLocation.forSourceFolder(in.readUTF(), in.readUTF());
                      break;
                  case 2 :
                      newState.classpathLocations[i] = ClasspathLocation.forBinaryFolder(in.readUTF());
                      break;
                  case 3 :
                      newState.classpathLocations[i] = ClasspathLocation.forLibrary(in.readUTF()); } }
       newState.structuralBuildNumbers = new SimpleLookupTable(length = in.readInt());
       for (int i = 0; i < length; i++)
             newState.structuralBuildNumbers.put(in.readUTF(), new Integer(in.readInt()));
       String[] internedLocations = new String[length = in.readInt()];
       for (int i = 0; i < length; i++)
             internedLocations[i] = in.readUTF();
       newState.typeLocations = new SimpleLookupTable(length = in.readInt());
       for (int i = 0; i < length; i++)
             newState.typeLocations.put(in.readUTF(), internedLocations[in.readInt()]);
       char[][] internedSimpleNames = ReferenceCollection.internSimpleNames(readNames(in), false);
       char[][][] internedQualifiedNames = new char[length = in.readInt()][][];
       for (int i = 0; i < length; i++) {
             int qLength = in.readInt();
             char[][] qName = new char[qLength][];
             for (int j = 0; j < qLength; j++)
                  qName[j] = internedSimpleNames[in.readInt()];
             internedQualifiedNames[i] = qName; }
       internedQualifiedNames = ReferenceCollection.internQualifiedNames(internedQualifiedNames);
       newState.references = new SimpleLookupTable(length = in.readInt());
       for (int i = 0; i < length; i++) {
             String location = internedLocations[in.readInt()];
             ReferenceCollection collection = null;
             switch (in.readByte()) {
                  case 1 :
                      char[][] additionalTypeNames = readNames(in);
                      char[][][] qualifiedNames = new char[in.readInt()][][];
                      for (int j = 0, qLength = qualifiedNames.length; j < qLength; j++)
                         qualifiedNames[j] = internedQualifiedNames[in.readInt()];
                      char[][] simpleNames = new char[in.readInt()][];
                      for (int j = 0, sLength = simpleNames.length; j < sLength; j++)
                         simpleNames[j] = internedSimpleNames[in.readInt()];
                      collection = new AdditionalTypeCollection(additionalTypeNames, qualifiedNames, simpleNames);
                      break;
                  case 2 :
                      char[][][] qNames = new char[in.readInt()][][];
                      for (int j = 0, qLength = qNames.length; j < qLength; j++)
                         qNames[j] = internedQualifiedNames[in.readInt()];
                      char[][] sNames = new char[in.readInt()][];
                      for (int j = 0, sLength = sNames.length; j < sLength; j++)
                         sNames[j] = internedSimpleNames[in.readInt()];
                      collection = new ReferenceCollection(qNames, sNames); }
             newState.references.put(location, collection); }
       if (JavaBuilder.DEBUG)
             System.out.println("Successfully read state for " + newState.javaProjectName); //$NON-NLS-1$
       return newState; }
private static char[][] readNames(DataInputStream in) throws IOException {
       int length = in.readInt();
       char[][] names = new char[length][];
       for (int i = 0; i < length; i++) {
             int nLength = in.readInt();
             char[] name = new char[nLength];
             for (int j = 0; j < nLength; j++)
                  name[j] = in.readChar();
             names[i] = name; }
       return names; }
void write(DataOutputStream out) throws IOException {
       int length;
       Object[] keyTable;
       Object[] valueTable;
/*
 * byte                     VERSION
 * String           project name
 * int                      build number
 * int                      last structural build number
 * String           output location
*/
       out.writeByte(VERSION);
       out.writeUTF(javaProjectName);
       out.writeInt(buildNumber);
       out.writeInt(lastStructuralBuildNumber);
       out.writeUTF(outputLocationString);
/*
 * Class path locations[]
 * int                      id
 * String           path(s)
*/
       out.writeInt(length = classpathLocations.length);
       for (int i = 0; i < length; ++i) {
             ClasspathLocation c = classpathLocations[i];
             if (c instanceof ClasspathMultiDirectory) {
                  out.writeByte(1);
                  ClasspathMultiDirectory md = (ClasspathMultiDirectory) c;
                  out.writeUTF(md.sourcePath);
                  out.writeUTF(md.binaryPath);
             } else if (c instanceof ClasspathDirectory) {
                  out.writeByte(2);
                  out.writeUTF(((ClasspathDirectory) c).binaryPath);
             } else if (c instanceof ClasspathJar) {
                  out.writeByte(3);
                  out.writeUTF(((ClasspathJar) c).zipFilename); } }
/*
 * Structural build numbers table
 * String           prereq project name
 * int                      last structural build number
*/
       out.writeInt(length = structuralBuildNumbers.elementSize);
       if (length > 0) {
             keyTable = structuralBuildNumbers.keyTable;
             valueTable = structuralBuildNumbers.valueTable;
             for (int i = 0, l = keyTable.length; i < l; i++) {
                  if (keyTable[i] != null) {
                      length--;
                      out.writeUTF((String) keyTable[i]);
                      out.writeInt(((Integer) valueTable[i]).intValue()); } }
             if (JavaBuilder.DEBUG && length != 0)
                  System.out.println("structuralBuildNumbers table is inconsistent");  }//$NON-NLS-1$
/*
 * String[]       Interned locations
 */
       out.writeInt(length = references.elementSize);
       ArrayList internedLocations = new ArrayList(length);
       if (length > 0) {
             keyTable = references.keyTable;
             for (int i = 0, l = keyTable.length; i < l; i++) {
                  if (keyTable[i] != null) {
                      length--;
                      String key = (String) keyTable[i];
                      out.writeUTF(key);
                      internedLocations.add(key); } }
             if (JavaBuilder.DEBUG && length != 0)
                  System.out.println("references table is inconsistent");  }//$NON-NLS-1$
/*
 * Type locations table
 * String           type name
 * int                      interned location id
 */
       out.writeInt(length = typeLocations.elementSize);
       if (length > 0) {
             keyTable = typeLocations.keyTable;
             valueTable = typeLocations.valueTable;
             for (int i = 0, l = keyTable.length; i < l; i++) {
                  if (keyTable[i] != null) {
                      length--;
                      out.writeUTF((String) keyTable[i]);
                      out.writeInt(internedLocations.indexOf((String) valueTable[i])); } }
             if (JavaBuilder.DEBUG && length != 0)
                  System.out.println("typeLocations table is inconsistent");  }//$NON-NLS-1$
/*
 * char[][][]  Interned qualified names
 * char[][]       Interned simple names
 */
       ArrayList internedQualifiedNames = new ArrayList(31);
       ArrayList internedSimpleNames = new ArrayList(31);
       valueTable = references.valueTable;
       for (int i = 0, l = valueTable.length; i < l; i++) {
             if (valueTable[i] != null) {
                  ReferenceCollection collection = (ReferenceCollection) valueTable[i];
                  char[][][] qNames = collection.qualifiedNameReferences;
                  for (int j = 0, qLength = qNames.length; j < qLength; j++) {
                      char[][] qName = qNames[j];
                      if (!internedQualifiedNames.contains(qName)) { // remember the names have been interned
                         internedQualifiedNames.add(qName);
                         for (int k = 0, sLength = qName.length; k < sLength; k++) {
                           char[] sName = qName[k];
                           if (!internedSimpleNames.contains(sName)) // remember the names have been interned
                            internedSimpleNames.add(sName); } } }
                  char[][] sNames = collection.simpleNameReferences;
                  for (int j = 0, sLength = sNames.length; j < sLength; j++) {
                      char[] sName = sNames[j];
                      if (!internedSimpleNames.contains(sName)) // remember the names have been interned
                         internedSimpleNames.add(sName); } } }
       char[][] internedArray = new char[internedSimpleNames.size()][];
       internedSimpleNames.toArray(internedArray);
       writeNames(internedArray, out);
       // now write the interned qualified names as arrays of interned simple names
       out.writeInt(length = internedQualifiedNames.size());
       for (int i = 0; i < length; i++) {
             char[][] qName = (char[][]) internedQualifiedNames.get(i);
             int qLength = qName.length;
             out.writeInt(qLength);
             for (int j = 0; j < qLength; j++)
                  out.writeInt(internedSimpleNames.indexOf(qName[j])); }
/*
 * References table
 * int                interned location id
 * ReferenceCollection
*/
       out.writeInt(length = references.elementSize);
       if (length > 0) {
             keyTable = references.keyTable;
             for (int i = 0, l = keyTable.length; i < l; i++) {
                  if (keyTable[i] != null) {
                      length--;
                      out.writeInt(internedLocations.indexOf((String) keyTable[i]));
                      ReferenceCollection collection = (ReferenceCollection) valueTable[i];
                      if (collection instanceof AdditionalTypeCollection) {
                         out.writeByte(1);
                         AdditionalTypeCollection atc = (AdditionalTypeCollection) collection;
                         writeNames(atc.definedTypeNames, out);
                      } else {
                         out.writeByte(2); }
                      char[][][] qNames = collection.qualifiedNameReferences;
                      int qLength = qNames.length;
                      out.writeInt(qLength);
                      for (int j = 0; j < qLength; j++)
                         out.writeInt(internedQualifiedNames.indexOf(qNames[j]));
                      char[][] sNames = collection.simpleNameReferences;
                      int sLength = sNames.length;
                      out.writeInt(sLength);
                      for (int j = 0; j < sLength; j++)
                         out.writeInt(internedSimpleNames.indexOf(sNames[j])); } }
             if (JavaBuilder.DEBUG && length != 0)
                  System.out.println("references table is inconsistent");  } }//$NON-NLS-1$
private void writeNames(char[][] names, DataOutputStream out) throws IOException {
       int length = names.length;
       out.writeInt(length);
       for (int i = 0; i < length; i++) {
             char[] name = names[i];
             int nLength = name.length;
             out.writeInt(nLength);
             for (int j = 0; j < nLength; j++)
                  out.writeChar(name[j]); } }
/**
 * Returns a string representation of the receiver.
 */
public String toString() {
       return "State for " + javaProjectName //$NON-NLS-1$
             + " (" + buildNumber //$NON-NLS-1$
                  + " : " + lastStructuralBuildNumber //$NON-NLS-1$
                      + ")";  }//$NON-NLS-1$
/* Debug helper
void dump() {
       System.out.println("State for " + javaProjectName + " (" + buildNumber + " : " + lastStructuralBuildNumber + ")");
       System.out.println("\tClass path locations:");
       for (int i = 0, length = classpathLocations.length; i < length; ++i)
             System.out.println("\t\t" + classpathLocations[i]);
       System.out.println("\tOutput location:");
       System.out.println("\t\t" + outputLocationString);
       System.out.print("\tStructural build numbers table:");
       if (structuralBuildNumbers.elementSize == 0) {
             System.out.print(" <empty>");
       } else {
             Object[] keyTable = structuralBuildNumbers.keyTable;
             Object[] valueTable = structuralBuildNumbers.valueTable;
             for (int i = 0, l = keyTable.length; i < l; i++)
                  if (keyTable[i] != null)
                      System.out.print("\n\t\t" + keyTable[i].toString() + " -> " + valueTable[i].toString()); }
       System.out.print("\tType locations table:");
       if (typeLocations.elementSize == 0) {
             System.out.print(" <empty>");
       } else {
             Object[] keyTable = typeLocations.keyTable;
             Object[] valueTable = typeLocations.valueTable;
             for (int i = 0, l = keyTable.length; i < l; i++)
                  if (keyTable[i] != null)
                      System.out.print("\n\t\t" + keyTable[i].toString() + " -> " + valueTable[i].toString()); }
       System.out.print("\n\tReferences table:");
       if (references.elementSize == 0) {
             System.out.print(" <empty>");
       } else {
             Object[] keyTable = references.keyTable;
             Object[] valueTable = references.valueTable;
             for (int i = 0, l = keyTable.length; i < l; i++) {
                  if (keyTable[i] != null) {
                      System.out.print("\n\t\t" + keyTable[i].toString());
                      ReferenceCollection c = (ReferenceCollection) valueTable[i];
                      char[][][] qRefs = c.qualifiedNameReferences;
                      System.out.print("\n\t\t\tqualified:");
                      if (qRefs.length == 0)
                         System.out.print(" <empty>");
                      else for (int j = 0, k = qRefs.length; j < k; j++)
                           System.out.print("  '" + org.eclipse.jdt.internal.compiler.util.CharOperation.toString(qRefs[j]) + "'");
                      char[][] sRefs = c.simpleNameReferences;
                      System.out.print("\n\t\t\tsimple:");
                      if (sRefs.length == 0)
                         System.out.print(" <empty>");
                      else for (int j = 0, k = sRefs.length; j < k; j++)
                           System.out.print("  " + new String(sRefs[j]));
                      if (c instanceof AdditionalTypeCollection) {
                         char[][] names = ((AdditionalTypeCollection) c).definedTypeNames;
                         System.out.print("\n\t\t\tadditional type names:");
                         for (int j = 0, k = names.length; j < k; j++)
                           System.out.print("  " + new String(names[j])); } } } }
       System.out.print("\n\n"); }
*/ }
