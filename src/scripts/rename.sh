#!/bin/bash 
# Generic NiCad renaming script
#
# Usage:  Rename granularity language pcfile.xml renaming 
#           where  granularity is one of:  { functions blocks ... }
#           and    language    is one of:  { c java cs py ... }
#           and    pcfile.xml is an edtracted potential clones file
#           and    renaming    is one of:  { blind, consistent }

# Revised 1.10.18

ulimit -s hard

# Find our installation
lib="${0%%/scripts/rename.sh}"
if [ ! -d ${lib} ]
then
    echo "*** Error:  cannot find NiCad installation ${lib}"
    echo ""
    exit 99
fi

# check granularity
if [ "$1" != "" ]
then
    granularity=$1
    shift
else
    echo "Usage:  Rename granularity language pcfile.xml renaming "
    echo "          where  granularity is one of:  { functions blocks ... }"
    echo "          and    language    is one of:  { c java cs py ... }"
    echo "          and    pcfile.xml is an edtracted potential clones file"
    echo "          and    renaming    is one of:  { blind, consistent }"
    exit 99
fi

# check language
if [ "$1" != "" ]
then
    language=$1
    shift
else
    echo "Usage:  Rename granularity language pcfile.xml renaming "
    echo "          where  granularity is one of:  { functions blocks ... }"
    echo "          and    language    is one of:  { c java cs py ... }"
    echo "          and    pcfile.xml is an edtracted potential clones file"
    echo "          and    renaming    is one of:  { blind, consistent }"
    exit 99
fi

# check we have a potential clones file
if [ "$1" != "" ]
then
    pcfile="${1%%.xml}"
    shift
else
    pcfile=""
fi

if [ ! -s "${pcfile}.xml" ]
then
    echo "Usage:  Rename granularity language pcfile.xml renaming "
    echo "          where  granularity is one of:  { functions blocks ... }"
    echo "          and    language    is one of:  { c java cs py ... }"
    echo "          and    pcfile.xml is an edtracted potential clones file"
    echo "          and    renaming    is one of:  { blind, consistent }"
    exit 99
fi

# check renaming
if [ "$1" = "blind" ] || [ "$1" = "consistent" ]
then
    renaming=$1
    shift
else
    echo "Usage:  Rename granularity language pcfile.xml renaming "
    echo "          where  granularity is one of:  { functions blocks ... }"
    echo "          and    language    is one of:  { c java cs py ... }"
    echo "          and    pcfile.xml is an edtracted potential clones file"
    echo "          and    renaming    is one of:  { blind, consistent }"
    exit 99
fi

# check we have the renamer we need
if [ ! -x ${lib}/txl/${language}-rename-${renaming}-${granularity}.x ]
then
    echo "*** ERROR: ${renaming} renaming not supported for ${language} ${granularity}"
    exit 99
fi

# Clean up any previous results
/bin/rm -f "${pcfile}-${renaming}.xml"

# Rename potential clones
date

echo "${lib}/tools/streamprocess.x '${lib}/txl/${language}-rename-${renaming}-${granularity}.x stdin' < ${pcfile}.xml > ${pcfile}-${renaming}.xml"
time ${lib}/tools/streamprocess.x "(${lib}/txl/${language}-rename-${renaming}-${granularity}.x stdin || cat)" < "${pcfile}.xml" > "${pcfile}-${renaming}.xml"

result=$?

echo ""
date
echo ""

exit $result
