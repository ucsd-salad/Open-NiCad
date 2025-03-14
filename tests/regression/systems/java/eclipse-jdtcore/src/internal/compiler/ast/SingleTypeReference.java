package org.eclipse.jdt.internal.compiler.ast;
/*
 * (c) Copyright IBM Corp. 2000, 2001.
 * All Rights Reserved.
 */
import org.eclipse.jdt.internal.compiler.IAbstractSyntaxTreeVisitor;
import org.eclipse.jdt.internal.compiler.impl.*;
import org.eclipse.jdt.internal.compiler.lookup.*;
public class SingleTypeReference extends TypeReference {
       public char[] token;
public SingleTypeReference(char[] source, long pos) {
             token = source;
             sourceStart = (int) (pos>>>32)  ;
             sourceEnd = (int) (pos & 0x00000000FFFFFFFFL) ; }
public SingleTypeReference(char[] source ,TypeBinding tb, long pos) {
       this(source, pos) ;
       binding = tb ; }
public TypeReference copyDims(int dim){
       //return a type reference copy of me with some dimensions
       //warning : the new type ref has a null binding
       return new ArrayTypeReference(token,null,dim,(((long)sourceStart)<<32)+sourceEnd) ; }
public TypeBinding getTypeBinding(Scope scope) {
       if (binding != null)
             return binding;
       return scope.getType(token); }
public char [][] getTypeName() {
       return new char[][] { token }; }
public TypeBinding resolveTypeEnclosing(BlockScope scope, ReferenceBinding enclosingType) {
       ReferenceBinding memberTb = scope.getMemberType(token, enclosingType);
       if (!memberTb.isValidBinding()) {
             scope.problemReporter().invalidEnclosingType(this, memberTb, enclosingType);
             return null; }
       if (isTypeUseDeprecated(memberTb, scope))
             scope.problemReporter().deprecatedType(memberTb, this);
       return binding = memberTb; }
public String toStringExpression(int tab){
       return new String(token) ; }
public void traverse(IAbstractSyntaxTreeVisitor visitor, BlockScope scope) {
       visitor.visit(this, scope);
       visitor.endVisit(this, scope); }
public void traverse(IAbstractSyntaxTreeVisitor visitor, ClassScope scope) {
       visitor.visit(this, scope);
       visitor.endVisit(this, scope); } }
