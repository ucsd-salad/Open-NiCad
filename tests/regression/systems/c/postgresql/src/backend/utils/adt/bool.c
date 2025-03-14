/*-------------------------------------------------------------------------
 *
 * bool.c
 *       Functions for the built-in type "bool".
 *
 * Portions Copyright (c) 1996-2001, PostgreSQL Global Development Group
 * Portions Copyright (c) 1994, Regents of the University of California
 *
 *
 * IDENTIFICATION
 *       $Header: /cvsroot/pgsql/src/backend/utils/adt/bool.c,v 1.25 2001/03/22 03:59:49 momjian Exp $
 *
 *-------------------------------------------------------------------------
 */
#include "postgres.h"
#include "utils/builtins.h"
/*****************************************************************************
 *      USER I/O ROUTINES                                                                  *
 *****************************************************************************/
/*
 *         boolin            - converts "t" or "f" to 1 or 0
 *
 * Check explicitly for "true/false" and TRUE/FALSE, 1/0, YES/NO.
 * Reject other values. - thomas 1997-10-05
 *
 * In the switch statement, check the most-used possibilities first.
 */
Datum
boolin(PG_FUNCTION_ARGS) {
       char     *b = PG_GETARG_CSTRING(0);
       switch (*b) {
             case 't':
             case 'T':
                  if (strncasecmp(b, "true", strlen(b)) == 0)
                      PG_RETURN_BOOL(true);
                  break;
             case 'f':
             case 'F':
                  if (strncasecmp(b, "false", strlen(b)) == 0)
                      PG_RETURN_BOOL(false);
                  break;
             case 'y':
             case 'Y':
                  if (strncasecmp(b, "yes", strlen(b)) == 0)
                      PG_RETURN_BOOL(true);
                  break;
             case '1':
                  if (strncasecmp(b, "1", strlen(b)) == 0)
                      PG_RETURN_BOOL(true);
                  break;
             case 'n':
             case 'N':
                  if (strncasecmp(b, "no", strlen(b)) == 0)
                      PG_RETURN_BOOL(false);
                  break;
             case '0':
                  if (strncasecmp(b, "0", strlen(b)) == 0)
                      PG_RETURN_BOOL(false);
                  break;
             default:
                  break; }
       elog(ERROR, "Bad boolean external representation '%s'", b);
       /* not reached */
       PG_RETURN_BOOL(false); }
/*
 *         boolout         - converts 1 or 0 to "t" or "f"
 */
Datum
boolout(PG_FUNCTION_ARGS) {
       bool   b = PG_GETARG_BOOL(0);
       char     *result = (char *) palloc(2);
       result[0] = (b) ? 't' : 'f';
       result[1] = '\0';
       PG_RETURN_CSTRING(result); }
/*****************************************************************************
 *      PUBLIC ROUTINES                                                              *
 *****************************************************************************/
Datum
booleq(PG_FUNCTION_ARGS) {
       bool   arg1 = PG_GETARG_BOOL(0);
       bool   arg2 = PG_GETARG_BOOL(1);
       PG_RETURN_BOOL(arg1 == arg2); }
Datum
boolne(PG_FUNCTION_ARGS) {
       bool   arg1 = PG_GETARG_BOOL(0);
       bool   arg2 = PG_GETARG_BOOL(1);
       PG_RETURN_BOOL(arg1 != arg2); }
Datum
boollt(PG_FUNCTION_ARGS) {
       bool   arg1 = PG_GETARG_BOOL(0);
       bool   arg2 = PG_GETARG_BOOL(1);
       PG_RETURN_BOOL(arg1 < arg2); }
Datum
boolgt(PG_FUNCTION_ARGS) {
       bool   arg1 = PG_GETARG_BOOL(0);
       bool   arg2 = PG_GETARG_BOOL(1);
       PG_RETURN_BOOL(arg1 > arg2); }
Datum
boolle(PG_FUNCTION_ARGS) {
       bool   arg1 = PG_GETARG_BOOL(0);
       bool   arg2 = PG_GETARG_BOOL(1);
       PG_RETURN_BOOL(arg1 <= arg2); }
Datum
boolge(PG_FUNCTION_ARGS) {
       bool   arg1 = PG_GETARG_BOOL(0);
       bool   arg2 = PG_GETARG_BOOL(1);
       PG_RETURN_BOOL(arg1 >= arg2); }
/*
 * Per SQL92, istrue() and isfalse() should return false, not NULL,
 * when presented a NULL input (since NULL is our implementation of
 * UNKNOWN).  Conversely isnottrue() and isnotfalse() should return true.
 * Therefore, these routines are all declared not-strict in pg_proc
 * and must do their own checking for null inputs.
 *
 * Note we don't need isunknown() and isnotunknown() functions, since
 * nullvalue() and nonnullvalue() will serve.
 */
Datum
istrue(PG_FUNCTION_ARGS) {
       bool   b;
       if (PG_ARGISNULL(0))
             PG_RETURN_BOOL(false);
       b = PG_GETARG_BOOL(0);
       PG_RETURN_BOOL(b); }
Datum
isfalse(PG_FUNCTION_ARGS) {
       bool   b;
       if (PG_ARGISNULL(0))
             PG_RETURN_BOOL(false);
       b = PG_GETARG_BOOL(0);
       PG_RETURN_BOOL(!b); }
Datum
isnottrue(PG_FUNCTION_ARGS) {
       bool   b;
       if (PG_ARGISNULL(0))
             PG_RETURN_BOOL(true);
       b = PG_GETARG_BOOL(0);
       PG_RETURN_BOOL(!b); }
Datum
isnotfalse(PG_FUNCTION_ARGS) {
       bool   b;
       if (PG_ARGISNULL(0))
             PG_RETURN_BOOL(true);
       b = PG_GETARG_BOOL(0);
       PG_RETURN_BOOL(b); }
