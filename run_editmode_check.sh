#!/bin/bash
UNITY="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
[ -f "$UNITY" ] || { echo "UNITY NOT FOUND: $UNITY"; exit 1; }
mkdir -p TestOutput
WINRES="$(wslpath -w /mnt/c/Unity/code/TestOutput/editmode-results.xml)"
WINLOG="$(wslpath -w /mnt/c/Unity/code/TestOutput/editmode.log)"
WINPROJ="$(wslpath -w /mnt/c/Unity/code)"
"$UNITY" -batchmode -projectPath "$WINPROJ" -runTests -testPlatform EditMode -testResults "$WINRES" -logFile "$WINLOG" 2>&1 | tail -3
echo "=== TESTS ==="
python3 -c "
import xml.etree.ElementTree as ET
r=ET.parse('TestOutput/editmode-results.xml').getroot()
for label, attr in [('tests','total'), ('passed','passed'), ('failures','failed'), ('errors','inconclusive'), ('skipped','skipped')]:
    print(label,'=',r.get(attr))
for tc in r.iter('test-case'):
    if tc.get('result') not in ('Passed','Skipped','Inconclusive'):
        name = tc.get('fullname') or tc.get('name')
        print('FAIL:', name)
        message = tc.find('failure/message')
        if message is not None and message.text:
            print(message.text.strip())
" 2>&1
