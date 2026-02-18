@ECHO OFF

pushd %~dp0

REM Command file for Sphinx documentation

if "%SPHINXBUILD%" == "" (
	set SPHINXBUILD=sphinx-build
)
set SOURCEDIR_RU=ru
set SOURCEDIR_EN=en
set BUILDDIR=_build

if "%1" == "" goto help
if "%1" == "help" goto help
if "%1" == "clean" goto clean
if "%1" == "html" goto html
if "%1" == "html-en" goto html-en
if "%1" == "html-all" goto html-all
if "%1" == "livehtml" goto livehtml

%SPHINXBUILD% >NUL 2>NUL
if errorlevel 9009 (
	echo.
	echo.The 'sphinx-build' command was not found. Make sure you have Sphinx
	echo.installed, then set the SPHINXBUILD environment variable to point
	echo.to the full path of the 'sphinx-build' executable. Alternatively you
	echo.may add the Sphinx directory to PATH.
	echo.
	echo.If you don't have Sphinx installed, grab it from
	echo.https://www.sphinx-doc.org/
	exit /b 1
)

:help
echo Available targets:
echo   html       - Build Russian documentation
echo   html-en    - Build English documentation
echo   html-all   - Build both Russian and English documentation
echo   clean      - Clean build directory
echo   livehtml   - Build with live reload (Russian)
goto end

:clean
if exist %BUILDDIR% rmdir /s /q %BUILDDIR%
echo Build directory cleaned.
goto end

:html
REM Build Russian documentation
%SPHINXBUILD% -M html %SOURCEDIR_RU% %BUILDDIR%/html -c %SOURCEDIR_RU% %SPHINXOPTS% %O%
echo.
echo.Build finished! The HTML pages are in %BUILDDIR%/html.
goto end

:html-en
REM Build English documentation
%SPHINXBUILD% -M html %SOURCEDIR_EN% %BUILDDIR%/en/html -c %SOURCEDIR_EN% %SPHINXOPTS% %O%
echo.
echo.Build finished! The HTML pages are in %BUILDDIR%/en/html.
goto end

:html-all
call :html
call :html-en
echo.
echo.All documentation built!
goto end

:livehtml
%SPHINXBUILD% -M html %SOURCEDIR_RU% %BUILDDIR% -c %SOURCEDIR_RU% %SPHINXOPTS% -W --keep-going --watch %SOURCEDIR_RU%
goto end

:end
popd
