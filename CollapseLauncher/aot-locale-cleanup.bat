@echo off

set root=%~dpn1

call :deleteDir af-ZA
call :deleteDir am-ET
call :deleteDir ar-SA
call :deleteDir as-IN
call :deleteDir az-Latn-AZ
call :deleteDir bg-BG
call :deleteDir bn-IN
call :deleteDir bs-Latn-BA
call :deleteDir ca-ES
call :deleteDir ca-Es-VALENCIA
call :deleteDir cs-CZ
call :deleteDir cy-GB
call :deleteDir da-DK
call :deleteDir de-DE
call :deleteDir el-GR
call :deleteDir en-GB
call :deleteDir es-ES
call :deleteDir es-MX
call :deleteDir et-EE
call :deleteDir eu-ES
call :deleteDir fa-IR
call :deleteDir fi-FI
call :deleteDir fil-PH
call :deleteDir fr-CA
call :deleteDir fr-FR
call :deleteDir ga-IE
call :deleteDir gd-gb
call :deleteDir gl-ES
call :deleteDir gu-IN
call :deleteDir he-IL
call :deleteDir hi-IN
call :deleteDir hr-HR
call :deleteDir hu-HU
call :deleteDir hy-AM
call :deleteDir id-ID
call :deleteDir is-IS
call :deleteDir it-IT
call :deleteDir ja-JP
call :deleteDir ka-GE
call :deleteDir kk-KZ
call :deleteDir km-KH
call :deleteDir kn-IN
call :deleteDir kok-IN
call :deleteDir ko-KR
call :deleteDir lb-LU
call :deleteDir lo-LA
call :deleteDir lt-LT
call :deleteDir lv-LV
call :deleteDir mi-NZ
call :deleteDir mk-MK
call :deleteDir ml-IN
call :deleteDir mr-IN
call :deleteDir ms-MY
call :deleteDir mt-MT
call :deleteDir nb-NO
call :deleteDir ne-NP
call :deleteDir nl-NL
call :deleteDir nn-NO
call :deleteDir or-IN
call :deleteDir pa-IN
call :deleteDir pl-PL
call :deleteDir pt-BR
call :deleteDir pt-PT
call :deleteDir quz-PE
call :deleteDir ro-RO
call :deleteDir ru-RU
call :deleteDir sk-SK
call :deleteDir sl-SI
call :deleteDir sq-AL
call :deleteDir sr-Cyrl-BA
call :deleteDir sr-Cyrl-RS
call :deleteDir sr-Latn-RS
call :deleteDir sv-SE
call :deleteDir ta-IN
call :deleteDir te-IN
call :deleteDir th-TH
call :deleteDir tr-TR
call :deleteDir tt-RU
call :deleteDir ug-CN
call :deleteDir uk-UA
call :deleteDir ur-PK
call :deleteDir uz-Latn-UZ
call :deleteDir vi-VN
call :deleteDir zh-CN
call :deleteDir zh-TW
goto :EOF

:deleteDir
set thePath="%root%\%~n1"
echo Removing %thePath%
if /I exist %thePath% (
    rmdir /S /Q %thePath%
)

exit /B