#!/bin/bash 
# Generic NiCad custom normalization script
#
# Usage:  Normalize granularity language pcfile.xml normalization
#           where granularity is one of:  { functions blocks ... }
#           and   language    is one of:  { c java cs py ... }
#           and   pcfile.xml  is an extracted potential clones file
#           and   normalization is the name of a custom TXL normalizer

# Revised 1.10.18

ulimit -s hard

# Find our installation
lib="${0%%/scripts/normalize.sh}"
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
    echo "Usage:  Normalize granularity language pcfile.xml normalization"
    echo "          where granularity is one of:  { functions blocks ... }"
    echo "          and   language    is one of:  { c java cs py ... }"
    echo "          and   pcfile.xml  is an extracted potential clones file"
    echo "          and   normalization is the name of a custom TXL normalizer"
    exit 99
fi

# check language
if [ "$1" != "" ]
then
    language=$1
    shift
else
    echo "Usage:  Normalize granularity language pcfile.xml normalization"
    echo "          where granularity is one of:  { functions blocks ... }"
    echo "          and   language    is one of:  { c java cs py ... }"
    echo "          and   pcfile.xml  is an extracted potential clones file"
    echo "          and   normalization is the name of a custom TXL normalizer"
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
    echo "Usage:  Normalize granularity language pcfile.xml normalization"
    echo "          where granularity is one of:  { functions blocks ... }"
    echo "          and   language    is one of:  { c java cs py ... }"
    echo "          and   pcfile.xml  is an extracted potential clones file"
    echo "          and   normalization is the name of a custom TXL normalizer"
    exit 99
fi

# check we have a normalizer
if [ "$1" != "" ]
then
    normalizer=$1
else
    echo "Usage:  Normalize granularity language pcfile.xml normalization"
    echo "          where granularity is one of:  { functions blocks ... }"
    echo "          and   language    is one of:  { c java cs py ... }"
    echo "          and   pcfile.xml  is an extracted potential clones file"
    echo "          and   normalization is the name of a custom TXL normalizer"
    exit 99
fi 

# check we have the normalizer we need
if [ ! -s ${lib}/txl/${normalizer}.x ]
then
    echo "*** ERROR: Can't find custom normalizer ${lib}/txl/${normalizer}.txl"
    exit 99
fi

# Clean up any previous results
/bin/rm -f "${pcfile}-normalized.xml"

# Rename potential clones
date

echo "${lib}/tools/streamprocess.x '${lib}/txl/${normalizer}.x stdin' < ${pcfile}.xml > ${pcfile}-normalized.xml"
time ${lib}/tools/streamprocess.x "${lib}/txl/${normalizer}.x stdin" < "${pcfile}.xml" > "${pcfile}-normalized.xml"

result=$?

echo ""
date
echo ""

exit $result
