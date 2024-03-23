@echo off

where /q xstyler
if %ERRORLEVEL% neq 0 (
    echo xstyler is not found in your PATH.
    echo Install Xaml Styler console as a global tool by running the following command:
    echo dotnet tool install -g XamlStyler.Console
    pause
    exit
)

echo This tool will apply XamlStyler configured in settings.xamlstyler file for all Xamls in ./CollapseLauncher/XAMLs/ directory.
pause
xstyler --config ./settings.xamlstyler --directory ./CollapseLauncher/XAMLs/ --recursive
