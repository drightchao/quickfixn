pushd AcceptanceTest
    del TestResult.xml AcceptanceTests_40.html AcceptanceTests_41.xml AcceptanceTests_42.xml AcceptanceTests_43.xml AcceptanceTests_44.xml
    call runat release 5001 definitions/server/fix40/*.def cfg/at_40.cfg
    if ERRORLEVEL 1 set RESULT40=1
    echo "40 tests result: %RESULT40%"
    xsltproc.exe -o AcceptanceTests_40.html at.xsl TestResult.xml
    
    call runat release 5002 definitions/server/fix41/*.def cfg/at_41.cfg
    if ERRORLEVEL 1 set RESULT41=1
    echo "41 tests result: %RESULT41%"
    xsltproc.exe -o AcceptanceTests_41.html at.xsl TestResult.xml
    
    call runat release 5003 definitions/server/fix42/*.def cfg/at_42.cfg
    if ERRORLEVEL 1 set RESULT42=1
    echo "42 tests result: %RESULT42%"
    xsltproc.exe -o AcceptanceTests_42.html at.xsl TestResult.xml
    
    call runat release 5004 definitions/server/fix43/*.def cfg/at_43.cfg
    if ERRORLEVEL 1 set RESULT43=1
    echo "43 tests result: %RESULT43%"
    xsltproc.exe -o AcceptanceTests_43.html at.xsl TestResult.xml
    
    call runat release 5005 definitions/server/fix44/*.def cfg/at_44.cfg
    if ERRORLEVEL 1 set RESULT44=1
    echo "44 tests result: %RESULT44%"
    xsltproc.exe -o AcceptanceTests_44.html at.xsl TestResult.xml
popd

echo ""
echo ""
echo "test suite results (blank means success):"
echo "    40: %RESULT40%"
echo "    41: %RESULT41%"
echo "    42: %RESULT42%"
echo "    43: %RESULT43%"
echo "    44: %RESULT44%"

set /a RETURNVALUE=RESULT40+RESULT41+RESULT42+RESULT43+RESULT44

echo "Script returns: %RETURNVALUE%"
exit /B %RETURNVALUE%

