package org.eclipse.jdt.internal.compiler.codegen;
/*
 * (c) Copyright IBM Corp. 2000, 2001.
 * All Rights Reserved.
 */
import org.eclipse.jdt.internal.compiler.*;
public class LongCache {
       public long keyTable[];
       public int valueTable[]; 
       int elementSize;
       int threshold;
/**
 * Constructs a new, empty hashtable. A default capacity and
 * load factor is used. Note that the hashtable will automatically 
 * grow when it gets full.
 */
public LongCache() {
       this(13); }
/**
 * Constructs a new, empty hashtable with the specified initial
 * capacity.
 * @param initialCapacity int
 *  the initial number of buckets
 */
public LongCache(int initialCapacity) {
       elementSize = 0;
       threshold = (int) (initialCapacity * 0.66);
       keyTable = new long[initialCapacity];
       valueTable = new int[initialCapacity]; }
/**
 * Clears the hash table so that it has no more elements in it.
 */
public void clear() {
       for (int i = keyTable.length; --i >= 0;) {
             keyTable[i] = 0;
             valueTable[i] = 0; }
       elementSize = 0; }
/** Returns true if the collection contains an element for the key.
 *
 * @param key <CODE>long</CODE> the key that we are looking for
 * @return boolean
 * @see ConstantPoolCache#contains
 */
public boolean containsKey(long key) {
       int index = hash(key);
       while ((keyTable[index] != 0) || ((keyTable[index] == 0) &&(valueTable[index] != 0))) {
             if (keyTable[index] == key)
                  return true;
             index = (index + 1) % keyTable.length; }
       return false; }
/** Gets the object associated with the specified key in the
 * hashtable.
 * @param key <CODE>long</CODE> the specified key
 * @return int the element for the key or -1 if the key is not
 *  defined in the hash table.
 * @see ConstantPoolCache#put
 */
public int get(long key) {
       int index = hash(key);
       while ((keyTable[index] != 0) || ((keyTable[index] == 0) &&(valueTable[index] != 0))) {
             if (keyTable[index] == key)
                  return valueTable[index];
             index = (index + 1) % keyTable.length; }
       return -1; }
/**
 * Return a hashcode for the value of the key parameter.
 * @param key long
 * @return int the hash code corresponding to the key value
 * @see ConstantPoolCache#put
 */
public int hash(long key) {
       return ((int) key & 0x7FFFFFFF) % keyTable.length; }
/**
 * Puts the specified element into the hashtable, using the specified
 * key.  The element may be retrieved by doing a get() with the same key.
 * 
 * @param key <CODE>long</CODE> the specified key in the hashtable
 * @param value <CODE>int</CODE> the specified element
 * @return int value
 */
public int put(long key, int value) {
       int index = hash(key);
       while ((keyTable[index] != 0) || ((keyTable[index] == 0) && (valueTable[index] != 0))) {
             if (keyTable[index] == key)
                  return valueTable[index] = value;
             index = (index + 1) % keyTable.length; }
       keyTable[index] = key;
       valueTable[index] = value;
       // assumes the threshold is never equal to the size of the table
       if (++elementSize > threshold) {
             rehash(); }
       return value; }
/**
 * Rehashes the content of the table into a bigger table.
 * This method is called automatically when the hashtable's
 * size exceeds the threshold.
 */
private void rehash() {
       LongCache newHashtable = new LongCache(keyTable.length * 2);
       for (int i = keyTable.length; --i >= 0;) {
             long key = keyTable[i];
             int value = valueTable[i];
             if ((key != 0) || ((key == 0) && (value != 0))) {
                  newHashtable.put(key, value); } }
       this.keyTable = newHashtable.keyTable;
       this.valueTable = newHashtable.valueTable;
       this.threshold = newHashtable.threshold; }
/**
 * Returns the number of elements contained in the hashtable.
 *
 * @return <CODE>int</CODE> The size of the table
 */
public int size() {
       return elementSize; }
/**
 * Converts to a rather lengthy String.
 *
 * return String the ascii representation of the receiver
 */
public String toString() {
       int max = size();
       StringBuffer buf = new StringBuffer();
       buf.append("{"); //$NON-NLS-1$
       for (int i = 0; i < max; ++i) {
             if ((keyTable[i] != 0) || ((keyTable[i] == 0) && (valueTable[i] != 0))) {
                  buf.append(keyTable[i]).append("->").append(valueTable[i]);  }//$NON-NLS-1$
             if (i < max) {
                  buf.append(", ");  } }//$NON-NLS-1$
       buf.append("}"); //$NON-NLS-1$
       return buf.toString(); } }
