;------------------------------------------------
;Kinovea Installer
;------------------------------------------------

;General
	SetCompressor /SOLID lzma
	!include "MUI.nsh"
	Name "Kinovea"
	!define VERSION "0.8.15"
	!define WORKINGDIR "Kinovea.Files"
	OutFile "Kinovea.Setup.${VERSION}.exe"
    

	;Default installation folder
	InstallDir "$PROGRAMFILES\Kinovea"
  
	;Get installation folder from registry if available 
	;Gère le cas où une ancienne version était installée ailleurs.
	InstallDirRegKey HKCU "Software\Kinovea" "InstallDirectory"

	BrandingText " "

	;Ask for admin privileges for UAC.
	RequestExecutionLevel admin
  
;--------------------------------
;Variables
;--------------------------------
  Var MUI_TEMP
  Var STARTMENU_FOLDER

;--------------------------------
;Interface Configuration
;--------------------------------
	;Icônes
	!define MUI_ICON "graphics\install.ico"
	!define MUI_UNICON "graphics\uninstall.ico"
	
	;Image on the header of the page. (150x57 pixels)
	!define MUI_HEADERIMAGE 
	!define MUI_HEADERIMAGE_BITMAP "graphics\150x57.bmp"
	
	;Bitmap for the Welcome page and the Finish page (164x314 pixels)
	!define MUI_WELCOMEFINISHPAGE_BITMAP "graphics\164x314.bmp"
	!define MUI_UNWELCOMEFINISHPAGE_BITMAP "graphics\164x314.bmp"
	
	;Show a message box with a warning when the user wants to close the installer.
	!define MUI_ABORTWARNING

;--------------------------------------
;Language Selection Dialog Settings
;--------------------------------------
  ;Remember the installer language
  !define MUI_LANGDLL_REGISTRY_ROOT "HKCU" 
  !define MUI_LANGDLL_REGISTRY_KEY "Software\Kinovea" 
  !define MUI_LANGDLL_REGISTRY_VALUENAME "Installer Language"

;----------------------------
;Pages configuration	
;----------------------------	
	;Start Menu Folder
	!define MUI_STARTMENUPAGE_REGISTRY_ROOT "HKCU" 
	!define MUI_STARTMENUPAGE_REGISTRY_KEY "Software\Kinovea" 
	!define MUI_STARTMENUPAGE_REGISTRY_VALUENAME "Start Menu Folder"
	;Finish
	!define MUI_FINISHPAGE_RUN "$INSTDIR\Kinovea.exe"

;----------------------------
;.NET Framework
;----------------------------
  !define MIN_FRA_MAJOR "2"
  !define MIN_FRA_MINOR "0"
  !define MIN_FRA_BUILD "*"



;--------------------------------
;Pages
;--------------------------------
	;Installer
	!insertmacro MUI_PAGE_WELCOME
	!insertmacro MUI_PAGE_LICENSE "${WORKINGDIR}\GPLv2.txt"
	!insertmacro MUI_PAGE_DIRECTORY
	!insertmacro MUI_PAGE_STARTMENU Application $STARTMENU_FOLDER
	!insertmacro MUI_PAGE_INSTFILES
	!insertmacro MUI_PAGE_FINISH

  
	;Uninstaller
	!insertmacro MUI_UNPAGE_CONFIRM
	!insertmacro MUI_UNPAGE_INSTFILES
	!insertmacro MUI_UNPAGE_FINISH


;--------------------------------
;Languages
;--------------------------------
  !insertmacro MUI_LANGUAGE "English" ;first language is the default language
  !insertmacro MUI_LANGUAGE "French"
  !insertmacro MUI_LANGUAGE "German"
  !insertmacro MUI_LANGUAGE "Spanish"
  ;!insertmacro MUI_LANGUAGE "SpanishInternational"
  !insertmacro MUI_LANGUAGE "SimpChinese"
  ;!insertmacro MUI_LANGUAGE "TradChinese"
  ;!insertmacro MUI_LANGUAGE "Japanese"
  ;!insertmacro MUI_LANGUAGE "Korean"
  !insertmacro MUI_LANGUAGE "Italian"
  !insertmacro MUI_LANGUAGE "Dutch"
  ;!insertmacro MUI_LANGUAGE "Danish"
  !insertmacro MUI_LANGUAGE "Swedish"
  !insertmacro MUI_LANGUAGE "Norwegian"
  ;!insertmacro MUI_LANGUAGE "NorwegianNynorsk"
  !insertmacro MUI_LANGUAGE "Finnish"
  !insertmacro MUI_LANGUAGE "Greek"
  ;!insertmacro MUI_LANGUAGE "Russian"
  !insertmacro MUI_LANGUAGE "Portuguese"
  ;!insertmacro MUI_LANGUAGE "PortugueseBR"
  !insertmacro MUI_LANGUAGE "Polish"
  ;!insertmacro MUI_LANGUAGE "Ukrainian"
  ;!insertmacro MUI_LANGUAGE "Czech"
  ;!insertmacro MUI_LANGUAGE "Slovak"
  ;!insertmacro MUI_LANGUAGE "Croatian"
  ;!insertmacro MUI_LANGUAGE "Bulgarian"
  ;!insertmacro MUI_LANGUAGE "Hungarian"
  ;!insertmacro MUI_LANGUAGE "Thai"
  !insertmacro MUI_LANGUAGE "Romanian"
  ;!insertmacro MUI_LANGUAGE "Latvian"
  ;!insertmacro MUI_LANGUAGE "Macedonian"
  ;!insertmacro MUI_LANGUAGE "Estonian"
  !insertmacro MUI_LANGUAGE "Turkish"
  !insertmacro MUI_LANGUAGE "Lithuanian"
  ;!insertmacro MUI_LANGUAGE "Slovenian"
  ;!insertmacro MUI_LANGUAGE "Serbian"
  ;!insertmacro MUI_LANGUAGE "SerbianLatin"
  ;!insertmacro MUI_LANGUAGE "Arabic"
  ;!insertmacro MUI_LANGUAGE "Farsi"
  ;!insertmacro MUI_LANGUAGE "Hebrew"
  ;!insertmacro MUI_LANGUAGE "Indonesian"
  ;!insertmacro MUI_LANGUAGE "Mongolian"
  ;!insertmacro MUI_LANGUAGE "Luxembourgish"
  ;!insertmacro MUI_LANGUAGE "Albanian"
  ;!insertmacro MUI_LANGUAGE "Breton"
  ;!insertmacro MUI_LANGUAGE "Belarusian"
  ;!insertmacro MUI_LANGUAGE "Icelandic"
  ;!insertmacro MUI_LANGUAGE "Malay"
  ;!insertmacro MUI_LANGUAGE "Bosnian"
  ;!insertmacro MUI_LANGUAGE "Kurdish"
  ;!insertmacro MUI_LANGUAGE "Irish"
  ;!insertmacro MUI_LANGUAGE "Uzbek"
  ;!insertmacro MUI_LANGUAGE "Galician"
  ;!insertmacro MUI_LANGUAGE "Afrikaans"
  ;!insertmacro MUI_LANGUAGE "Catalan"

  ;Language strings for the .NET framework warning.
  !include DotNetWarning.nsi
  
  ;Properties of the installer file
	VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductName" "Kinovea"
	VIAddVersionKey /LANG=${LANG_ENGLISH} "Comments" "Video Analysis"
	VIAddVersionKey /LANG=${LANG_ENGLISH} "CompanyName" "Kinovea"
	VIAddVersionKey /LANG=${LANG_ENGLISH} "LegalCopyright" "Copyright © 2006-2011 Joan Charmant"
	VIAddVersionKey /LANG=${LANG_ENGLISH} "FileDescription" "Kinovea Installer"
	VIAddVersionKey /LANG=${LANG_ENGLISH} "FileVersion" "${VERSION}"

	VIProductVersion "${VERSION}.0"
	
	CRCCheck on

;--------------------------------
;Reserve Files
  
  ;If you are using solid compression, files that are required before
  ;the actual installation should be stored first in the data block,
  ;because this will make your installer start faster.
  
  !insertmacro MUI_RESERVEFILE_LANGDLL

;--------------------------------
;Installer Sections

;In a common installer there are several things the user can install. 
;For example in the NSIS distribution installer you can choose to install the source code, additional plug-ins, examples and more. 
;Each of these components has its own piece of code. 
;If the user selects to install this component, then the installer will execute that code. 
;In the script, that code is defined in sections. 
;Each section corresponds to one component in the components page. 
;The section's name is the displayed component name, and the section code will be executed if that component is selected. 
;It is possible to build your installer with only one section, but if you want to use the components page and let the user choose what to install, you'll have to use more than one section.


;Main installer section.
Section ""
  
	; todo : terminate app.
  
	SetOutPath "$INSTDIR"
	
	File "${WORKINGDIR}\Kinovea.exe"
	File "${WORKINGDIR}\Kinovea.FileBrowser.dll"
	File "${WORKINGDIR}\Kinovea.ScreenManager.dll"
	File "${WORKINGDIR}\Kinovea.Services.dll"
	File "${WORKINGDIR}\Kinovea.Updater.dll"
	File "${WORKINGDIR}\PlayerServer.dll"
	
	File "${WORKINGDIR}\AForge.dll"
	File "${WORKINGDIR}\AForge.Imaging.dll"
	File "${WORKINGDIR}\AForge.Math.dll"
	File "${WORKINGDIR}\AForge.Video.dll"
	File "${WORKINGDIR}\AForge.Video.DirectShow.dll"
	
	File "${WORKINGDIR}\Emgu.CV.dll"
	File "${WORKINGDIR}\Emgu.Util.dll"
	File "${WORKINGDIR}\cvaux200.dll"
	File "${WORKINGDIR}\cv200.dll"
	File "${WORKINGDIR}\cxcore200.dll"
	
	File "${WORKINGDIR}\avcodec-52.dll"
	File "${WORKINGDIR}\avformat-52.dll" 
	File "${WORKINGDIR}\avutil-50.dll"
	File "${WORKINGDIR}\swscale-0.dll"
	
	File "${WORKINGDIR}\SharpVectorBindings.dll"
	File "${WORKINGDIR}\SharpVectorCss.dll"
	File "${WORKINGDIR}\SharpVectorDom.dll"
	File "${WORKINGDIR}\SharpVectorObjectModel.dll"
	File "${WORKINGDIR}\SharpVectorUtil.dll"
	File "${WORKINGDIR}\SharpVectorRenderingEngine.dll"
	
	File "${WORKINGDIR}\Microsoft.VC90.CRT.manifest"
	File "${WORKINGDIR}\msvcm90.dll"
	File "${WORKINGDIR}\msvcp90.dll"
	File "${WORKINGDIR}\msvcr90.dll"
	File "${WORKINGDIR}\Microsoft.VC90.OpenMP.manifest"
	File "${WORKINGDIR}\vcomp90.dll"
	
	File "${WORKINGDIR}\ExpTreeLib.dll"
	File "${WORKINGDIR}\log4net.dll"
	File "${WORKINGDIR}\pthreadGC2.dll"
	File "${WORKINGDIR}\ICSharpCode.SharpZipLib.dll"
	
	File "${WORKINGDIR}\HelpIndex.xml"
	File "${WORKINGDIR}\LogConf.xml"
	File "${WORKINGDIR}\GPLv2.txt"
	File "${WORKINGDIR}\License.txt"
	File "${WORKINGDIR}\Readme.txt"
	
	CreateDirectory "$INSTDIR\xslt"
	CreateDirectory "$INSTDIR\guides"
	CreateDirectory "$INSTDIR\fr"
	CreateDirectory "$INSTDIR\nl"
	CreateDirectory "$INSTDIR\de"
	CreateDirectory "$INSTDIR\es"
	CreateDirectory "$INSTDIR\pt"
	CreateDirectory "$INSTDIR\pl"
	CreateDirectory "$INSTDIR\it"
	CreateDirectory "$INSTDIR\ro"
	CreateDirectory "$INSTDIR\fi"
	CreateDirectory "$INSTDIR\no"
	CreateDirectory "$INSTDIR\tr"
	CreateDirectory "$INSTDIR\Zh-CHS"
	CreateDirectory "$INSTDIR\el"
	CreateDirectory "$INSTDIR\lt"
	CreateDirectory "$INSTDIR\sv"
	CreateDirectory "$INSTDIR\HelpVideos"
	CreateDirectory "$INSTDIR\Manuals"
	
	SetOutPath "$INSTDIR\xslt"
		File "${WORKINGDIR}\xslt\kva-1.2to1.3.xsl"
		File "${WORKINGDIR}\xslt\kva2msxml-en.xsl"
		File "${WORKINGDIR}\xslt\kva2odf-en.xsl"
		File "${WORKINGDIR}\xslt\kva2txt-en.xsl"
		File "${WORKINGDIR}\xslt\kva2xhtml-en.xsl"
	
	SetOutPath "$INSTDIR\guides"
		File "${WORKINGDIR}\guides\circle - blue.svg"
		File "${WORKINGDIR}\guides\clock - degrees - blue.svg"
		File "${WORKINGDIR}\guides\foot.svg"
		File "${WORKINGDIR}\guides\hexaxial.svg"
		File "${WORKINGDIR}\guides\human skeleton.svg"
        File "${WORKINGDIR}\guides\protractor.svg"

	SetOutPath "$INSTDIR\fr"
		File "${WORKINGDIR}\fr\Kinovea.FileBrowser.resources.dll"
		File "${WORKINGDIR}\fr\Kinovea.resources.dll"
		File "${WORKINGDIR}\fr\Kinovea.ScreenManager.resources.dll"
		File "${WORKINGDIR}\fr\Kinovea.Updater.resources.dll"
		
	SetOutPath "$INSTDIR\nl"
		File "${WORKINGDIR}\nl\Kinovea.FileBrowser.resources.dll"
		File "${WORKINGDIR}\nl\Kinovea.resources.dll"
		File "${WORKINGDIR}\nl\Kinovea.ScreenManager.resources.dll"
		File "${WORKINGDIR}\nl\Kinovea.Updater.resources.dll"
		
	SetOutPath "$INSTDIR\de"
		File "${WORKINGDIR}\de\Kinovea.FileBrowser.resources.dll"
		File "${WORKINGDIR}\de\Kinovea.resources.dll"
		File "${WORKINGDIR}\de\Kinovea.ScreenManager.resources.dll"
		File "${WORKINGDIR}\de\Kinovea.Updater.resources.dll"
  
  SetOutPath "$INSTDIR\es"
		File "${WORKINGDIR}\es\Kinovea.FileBrowser.resources.dll"
		File "${WORKINGDIR}\es\Kinovea.resources.dll"
		File "${WORKINGDIR}\es\Kinovea.ScreenManager.resources.dll"
		File "${WORKINGDIR}\es\Kinovea.Updater.resources.dll"
		
	SetOutPath "$INSTDIR\pt"
		File "${WORKINGDIR}\pt\Kinovea.FileBrowser.resources.dll"
		File "${WORKINGDIR}\pt\Kinovea.resources.dll"
		File "${WORKINGDIR}\pt\Kinovea.ScreenManager.resources.dll"
		File "${WORKINGDIR}\pt\Kinovea.Updater.resources.dll"
	
	SetOutPath "$INSTDIR\pl"
		File "${WORKINGDIR}\pl\Kinovea.FileBrowser.resources.dll"
		File "${WORKINGDIR}\pl\Kinovea.resources.dll"
		File "${WORKINGDIR}\pl\Kinovea.ScreenManager.resources.dll"
		File "${WORKINGDIR}\pl\Kinovea.Updater.resources.dll"
		
	SetOutPath "$INSTDIR\it"
		File "${WORKINGDIR}\it\Kinovea.FileBrowser.resources.dll"
		File "${WORKINGDIR}\it\Kinovea.resources.dll"
		File "${WORKINGDIR}\it\Kinovea.ScreenManager.resources.dll"
		File "${WORKINGDIR}\it\Kinovea.Updater.resources.dll"
		
	SetOutPath "$INSTDIR\ro"
		File "${WORKINGDIR}\ro\Kinovea.FileBrowser.resources.dll"
		File "${WORKINGDIR}\ro\Kinovea.resources.dll"
		File "${WORKINGDIR}\ro\Kinovea.ScreenManager.resources.dll"
		File "${WORKINGDIR}\ro\Kinovea.Updater.resources.dll"
	
	SetOutPath "$INSTDIR\fi"
		File "${WORKINGDIR}\fi\Kinovea.FileBrowser.resources.dll"
		File "${WORKINGDIR}\fi\Kinovea.resources.dll"
		File "${WORKINGDIR}\fi\Kinovea.ScreenManager.resources.dll"
		File "${WORKINGDIR}\fi\Kinovea.Updater.resources.dll"
		
	SetOutPath "$INSTDIR\no"
		File "${WORKINGDIR}\no\Kinovea.FileBrowser.resources.dll"
		File "${WORKINGDIR}\no\Kinovea.resources.dll"
		File "${WORKINGDIR}\no\Kinovea.ScreenManager.resources.dll"
		File "${WORKINGDIR}\no\Kinovea.Updater.resources.dll"
		
	SetOutPath "$INSTDIR\tr"
		File "${WORKINGDIR}\tr\Kinovea.FileBrowser.resources.dll"
		File "${WORKINGDIR}\tr\Kinovea.resources.dll"
		File "${WORKINGDIR}\tr\Kinovea.ScreenManager.resources.dll"
		File "${WORKINGDIR}\tr\Kinovea.Updater.resources.dll"
		
	SetOutPath "$INSTDIR\Zh-CHS"
		File "${WORKINGDIR}\Zh-CHS\Kinovea.FileBrowser.resources.dll"
		File "${WORKINGDIR}\Zh-CHS\Kinovea.resources.dll"
		File "${WORKINGDIR}\Zh-CHS\Kinovea.ScreenManager.resources.dll"
		File "${WORKINGDIR}\Zh-CHS\Kinovea.Updater.resources.dll"
		
	SetOutPath "$INSTDIR\el"
		File "${WORKINGDIR}\el\Kinovea.FileBrowser.resources.dll"
		File "${WORKINGDIR}\el\Kinovea.resources.dll"
		File "${WORKINGDIR}\el\Kinovea.ScreenManager.resources.dll"
		File "${WORKINGDIR}\el\Kinovea.Updater.resources.dll"
		
	SetOutPath "$INSTDIR\lt"
		File "${WORKINGDIR}\lt\Kinovea.FileBrowser.resources.dll"
		File "${WORKINGDIR}\lt\Kinovea.resources.dll"
		File "${WORKINGDIR}\lt\Kinovea.ScreenManager.resources.dll"
		File "${WORKINGDIR}\lt\Kinovea.Updater.resources.dll"
		
	SetOutPath "$INSTDIR\sv"
		File "${WORKINGDIR}\sv\Kinovea.FileBrowser.resources.dll"
		File "${WORKINGDIR}\sv\Kinovea.resources.dll"
		File "${WORKINGDIR}\sv\Kinovea.ScreenManager.resources.dll"
		File "${WORKINGDIR}\sv\Kinovea.Updater.resources.dll"
		
  SetOutPath "$INSTDIR\HelpVideos"
		File "${WORKINGDIR}\HelpVideos\fr-visualisation-decouverte.avi"
		File "${WORKINGDIR}\HelpVideos\en-visualization-basics.avi"
  
  SetOutPath "$INSTDIR\Manuals"
		File "${WORKINGDIR}\Manuals\kinovea.fr.chm"
		File "${WORKINGDIR}\Manuals\kinovea.en.chm"
		File "${WORKINGDIR}\Manuals\kinovea.it.chm"
 
  ; Reset Output path for links working directory...
  SetOutPath "$INSTDIR"
  
	;Store installation folder
	WriteRegStr HKCU "Software\Kinovea" "InstallDirectory" $INSTDIR  
  
	;Create uninstaller
	WriteUninstaller "$INSTDIR\Uninstall.exe"
	
	;Register uninstaller to Windows.
	!define AppRemovePath "Software\Microsoft\Windows\CurrentVersion\Uninstall\Kinovea"
	WriteRegStr HKLM "${AppRemovePath}" "DisplayName" "Kinovea"
	WriteRegStr HKLM "${AppRemovePath}" "DisplayVersion" "${VERSION}"
	WriteRegDWORD HKLM "${AppRemovePath}" "NoModify" "1"
	WriteRegDWORD HKLM "${AppRemovePath}" "NoRepair" "1"
	WriteRegStr HKLM "${AppRemovePath}" "Publisher" "Kinovea"
	WriteRegStr HKLM "${AppRemovePath}" "UninstallString" "$\"$INSTDIR\Uninstall.exe$\""
	
	!insertmacro MUI_STARTMENU_WRITE_BEGIN Application
		;Create shortcuts under all users. 
		SetShellVarContext all
		
		CreateDirectory "$SMPROGRAMS\$STARTMENU_FOLDER"
		CreateShortcut "$SMPROGRAMS\$STARTMENU_FOLDER\Kinovea.lnk" "$INSTDIR\Kinovea.exe"
		CreateShortCut "$SMPROGRAMS\$STARTMENU_FOLDER\UninstallKinovea.lnk" "$INSTDIR\Uninstall.exe"
 
		;CreateDirectory "$SMPROGRAMS\$STARTMENU_FOLDER"
		;CreateShortCut "$SMPROGRAMS\$STARTMENU_FOLDER\Kinovea.lnk" 
		;CreateShortCut "$DESKTOP\Kinovea.lnk" "$INSTDIR\Kinovea.exe"
	!insertmacro MUI_STARTMENU_WRITE_END
  
SectionEnd




;----------------------------------------------------------------------------------------------------------------------
;Installer Functions
Function .onInit
	
  ; This will abort the install if the .NET framework is not suitable.
  ; For each and every locale we enable (user can choose in the drop down), we must define the error messages. (Else it will blank). 
  ; if the computer locale is not enabled, message will be displayed in english. 
  Call AbortIfBadFramework

  !insertmacro MUI_LANGDLL_DISPLAY
FunctionEnd

;----------------------------------------------------------------------------------------------------------------------
;Uninstaller Section
Section "Uninstall"
  
	; We delete the old versions files too.
	; We also delete generated files not created at installation (Prefs, ColorProfile)

	Delete "$INSTDIR\Uninstall.exe"
	Delete "$INSTDIR\Kinovea.exe"
	Delete "$INSTDIR\AForge.dll"
	Delete "$INSTDIR\AForge.Imaging.dll"
	Delete "$INSTDIR\AForge.Math.dll"
	Delete "$INSTDIR\AForge.Video.dll"
	Delete "$INSTDIR\AForge.Video.DirectShow.dll"
	Delete "$INSTDIR\Emgu.CV.dll"
	Delete "$INSTDIR\Emgu.Util.dll"
	Delete "$INSTDIR\cvaux200.dll"
	Delete "$INSTDIR\cv200.dll"
	Delete "$INSTDIR\cxcore200.dll"
	Delete "$INSTDIR\avcodec-51.dll"					;delete from <= 0.8.8
	Delete "$INSTDIR\avformat-51.dll" 				;delete from <= 0.7.6
	Delete "$INSTDIR\avcodec-52.dll"
	Delete "$INSTDIR\avformat-52.dll"
	Delete "$INSTDIR\avutil-49.dll"					;delete from <= 0.8.8
	Delete "$INSTDIR\avutil-50.dll"
	Delete "$INSTDIR\CPI.Plot3D.dll"				;delete from <= 0.7.10
	Delete "$INSTDIR\ExpTreeLib.dll"
	Delete "$INSTDIR\Kinovea.BaseScreen.dll"		;delete from <= 0.6.3
	Delete "$INSTDIR\Kinovea.FileBrowser.dll"
	Delete "$INSTDIR\Kinovea.PlayerScreen.dll"		;delete from <= 0.6.3
	Delete "$INSTDIR\Kinovea.ScreenManager.dll"
	Delete "$INSTDIR\Kinovea.Services.dll"
	Delete "$INSTDIR\Kinovea.Supervisor.dll"		;delete from <= 0.6.3
	Delete "$INSTDIR\Kinovea.Updater.dll"
	Delete "$INSTDIR\SharpVectorBindings.dll"
	Delete "$INSTDIR\SharpVectorCss.dll"
	Delete "$INSTDIR\SharpVectorDom.dll"
	Delete "$INSTDIR\SharpVectorObjectModel.dll"
	Delete "$INSTDIR\SharpVectorUtil.dll"
	Delete "$INSTDIR\SharpVectorRenderingEngine.dll"
	
	Delete "$INSTDIR\log4net.dll"
	Delete "$INSTDIR\msvcm80.dll"					;delete from <= 0.8.6
	Delete "$INSTDIR\msvcp80.dll"					;delete from <= 0.8.6
	Delete "$INSTDIR\msvcr80.dll"					;delete from <= 0.8.6
	Delete "$INSTDIR\msvcm90.dll"
	Delete "$INSTDIR\msvcp90.dll"
	Delete "$INSTDIR\msvcr90.dll"
	Delete "$INSTDIR\vcomp90.dll"
	Delete "$INSTDIR\PdfSharp.dll"					;delete from <= 0.7.10
	Delete "$INSTDIR\PlayerServer.dll"
	Delete "$INSTDIR\pthreadGC2.dll"
	Delete "$INSTDIR\swscale-0.dll"
	Delete "$INSTDIR\ICSharpCode.SharpZipLib.dll"
	Delete "$INSTDIR\Preferences.xml"							; Created dynamically at runtime
	
	
	Delete "$INSTDIR\Kinovea.exe.manifest"
	Delete "$INSTDIR\Microsoft.VC80.CRT.manifest"	;delete from <= 0.8.6
	Delete "$INSTDIR\Microsoft.VC90.CRT.manifest"
	Delete "$INSTDIR\Microsoft.VC90.OpenMP.manifest"
	Delete "$INSTDIR\HelpIndex.xml"
	Delete "$INSTDIR\LogConf.xml"
	Delete "$INSTDIR\GPLv2.txt"
	Delete "$INSTDIR\GFDL.txt"						;delete from <= 0.8.6
	Delete "$INSTDIR\CC-BY-ND-3.0.htm"				;delete from <= 0.6.2
	Delete "$INSTDIR\LAL-french.htm"				;delete from <= 0.8.6
	Delete "$INSTDIR\LAL-english.htm"				;delete from <= 0.8.6
	Delete "$INSTDIR\License.txt"
	Delete "$INSTDIR\Readme.txt"

	Delete "$INSTDIR\xslt\kva-1.2to1.3.xsl"
	Delete "$INSTDIR\xslt\kva2msxml-en.xsl"
	Delete "$INSTDIR\xslt\kva2odf-en.xsl"
	Delete "$INSTDIR\xslt\kva2txt-en.xsl"
	Delete "$INSTDIR\xslt\kva2xhtml-en.xsl"
	RMDir "$INSTDIR\xslt"

	Delete "$INSTDIR\guides\circle - blue.svg"
	Delete "$INSTDIR\guides\clock - degrees - blue.svg"
	Delete "$INSTDIR\guides\foot.svg"
	Delete "$INSTDIR\guides\hexaxial.svg"
	Delete "$INSTDIR\guides\human skeleton.svg"
	Delete "$INSTDIR\guides\protractor.svg"
	RMDir "$INSTDIR\guides"

	Delete "$INSTDIR\fr\Kinovea.FileBrowser.resources.dll"
	Delete "$INSTDIR\fr\Kinovea.PlayerScreen.resources.dll"		;delete from <= 0.6.3
	Delete "$INSTDIR\fr\Kinovea.resources.dll"
	Delete "$INSTDIR\fr\Kinovea.ScreenManager.resources.dll"
	Delete "$INSTDIR\fr\Kinovea.Supervisor.resources.dll"		;delete from <= 0.6.3
	Delete "$INSTDIR\fr\Kinovea.Updater.resources.dll"
	RMDir "$INSTDIR\fr"

	Delete "$INSTDIR\nl\Kinovea.FileBrowser.resources.dll"	
	Delete "$INSTDIR\nl\Kinovea.resources.dll"
	Delete "$INSTDIR\nl\Kinovea.ScreenManager.resources.dll"
	Delete "$INSTDIR\nl\Kinovea.Updater.resources.dll"
	RMDir "$INSTDIR\nl"
	
	Delete "$INSTDIR\de\Kinovea.FileBrowser.resources.dll"	
	Delete "$INSTDIR\de\Kinovea.resources.dll"
	Delete "$INSTDIR\de\Kinovea.ScreenManager.resources.dll"
	Delete "$INSTDIR\de\Kinovea.Updater.resources.dll"
	RMDir "$INSTDIR\de"
	
	Delete "$INSTDIR\es\Kinovea.FileBrowser.resources.dll"	
	Delete "$INSTDIR\es\Kinovea.resources.dll"
	Delete "$INSTDIR\es\Kinovea.ScreenManager.resources.dll"
	Delete "$INSTDIR\es\Kinovea.Updater.resources.dll"
	RMDir "$INSTDIR\es"
	
	Delete "$INSTDIR\pt\Kinovea.FileBrowser.resources.dll"	
	Delete "$INSTDIR\pt\Kinovea.resources.dll"
	Delete "$INSTDIR\pt\Kinovea.ScreenManager.resources.dll"
	Delete "$INSTDIR\pt\Kinovea.Updater.resources.dll"
	RMDir "$INSTDIR\pt"
	
	Delete "$INSTDIR\pl\Kinovea.FileBrowser.resources.dll"	
	Delete "$INSTDIR\pl\Kinovea.resources.dll"
	Delete "$INSTDIR\pl\Kinovea.ScreenManager.resources.dll"
	Delete "$INSTDIR\pl\Kinovea.Updater.resources.dll"
	RMDir "$INSTDIR\pl"
	
	Delete "$INSTDIR\it\Kinovea.FileBrowser.resources.dll"	
	Delete "$INSTDIR\it\Kinovea.resources.dll"
	Delete "$INSTDIR\it\Kinovea.ScreenManager.resources.dll"
	Delete "$INSTDIR\it\Kinovea.Updater.resources.dll"
	RMDir "$INSTDIR\it"
	
	Delete "$INSTDIR\ro\Kinovea.FileBrowser.resources.dll"	
	Delete "$INSTDIR\ro\Kinovea.resources.dll"
	Delete "$INSTDIR\ro\Kinovea.ScreenManager.resources.dll"
	Delete "$INSTDIR\ro\Kinovea.Updater.resources.dll"
	RMDir "$INSTDIR\ro"
	
	Delete "$INSTDIR\fi\Kinovea.FileBrowser.resources.dll"	
	Delete "$INSTDIR\fi\Kinovea.resources.dll"
	Delete "$INSTDIR\fi\Kinovea.ScreenManager.resources.dll"
	Delete "$INSTDIR\fi\Kinovea.Updater.resources.dll"
	RMDir "$INSTDIR\fi"
	
	Delete "$INSTDIR\no\Kinovea.FileBrowser.resources.dll"	
	Delete "$INSTDIR\no\Kinovea.resources.dll"
	Delete "$INSTDIR\no\Kinovea.ScreenManager.resources.dll"
	Delete "$INSTDIR\no\Kinovea.Updater.resources.dll"
	RMDir "$INSTDIR\no"
	
	Delete "$INSTDIR\tr\Kinovea.FileBrowser.resources.dll"	
	Delete "$INSTDIR\tr\Kinovea.resources.dll"
	Delete "$INSTDIR\tr\Kinovea.ScreenManager.resources.dll"
	Delete "$INSTDIR\tr\Kinovea.Updater.resources.dll"
	RMDir "$INSTDIR\tr"
	
	Delete "$INSTDIR\Zh-CHS\Kinovea.FileBrowser.resources.dll"	
	Delete "$INSTDIR\Zh-CHS\Kinovea.resources.dll"
	Delete "$INSTDIR\Zh-CHS\Kinovea.ScreenManager.resources.dll"
	Delete "$INSTDIR\Zh-CHS\Kinovea.Updater.resources.dll"
	RMDir "$INSTDIR\Zh-CHS"
	
	Delete "$INSTDIR\el\Kinovea.FileBrowser.resources.dll"	
	Delete "$INSTDIR\el\Kinovea.resources.dll"
	Delete "$INSTDIR\el\Kinovea.ScreenManager.resources.dll"
	Delete "$INSTDIR\el\Kinovea.Updater.resources.dll"
	RMDir "$INSTDIR\el"

	Delete "$INSTDIR\lt\Kinovea.FileBrowser.resources.dll"	
	Delete "$INSTDIR\lt\Kinovea.resources.dll"
	Delete "$INSTDIR\lt\Kinovea.ScreenManager.resources.dll"
	Delete "$INSTDIR\lt\Kinovea.Updater.resources.dll"
	RMDir "$INSTDIR\lt"
	
	Delete "$INSTDIR\sv\Kinovea.FileBrowser.resources.dll"	
	Delete "$INSTDIR\sv\Kinovea.resources.dll"
	Delete "$INSTDIR\sv\Kinovea.ScreenManager.resources.dll"
	Delete "$INSTDIR\sv\Kinovea.Updater.resources.dll"
	RMDir "$INSTDIR\sv"
	
	Delete "$INSTDIR\HelpVideos\fr-visualisation-decouverte.avi"
	Delete "$INSTDIR\HelpVideos\en-visualization-basics.avi"
	RMDir "$INSTDIR\HelpVideos"
	
	Delete "$INSTDIR\Manuals\kinovea.fr.chm"
	Delete "$INSTDIR\Manuals\kinovea.en.chm"
	Delete "$INSTDIR\Manuals\kinovea.it.chm"
	RMDir "$INSTDIR\Manuals"
	
	Delete "$INSTDIR\ColorProfiles\current.xml"							; Created dynamically at runtime
	RMDir "$INSTDIR\ColorProfiles"										; Won't work if user saved something in there.		
	
	RMDir "$INSTDIR"


  !insertmacro MUI_STARTMENU_GETFOLDER Application $MUI_TEMP  
  
  ;Remove user data
  SetShellVarContext current
  Delete "$APPDATA\Kinovea\log.txt"
  Delete "$APPDATA\Kinovea\Preferences.xml"
  RMDir /r "$APPDATA\Kinovea"
    
  Delete "$SMPROGRAMS\$MUI_TEMP\Kinovea Uninstall.lnk"					;delete from <= 0.7.8
  Delete "$SMPROGRAMS\$MUI_TEMP\Kinovea.lnk"							;delete from <= 0.7.8
  Delete "$DESKTOP\Kinovea.lnk"					
  ;Todo: delete kinovea folder under start menu / programs of current user.  
    
  ;Remove global shortcuts.
  SetShellVarContext all
  Delete "$SMPROGRAMS\$MUI_TEMP\UninstallKinovea.lnk"
  Delete "$SMPROGRAMS\$MUI_TEMP\Kinovea.lnk"
  
  
  ;Delete empty start menu parent directories
  StrCpy $MUI_TEMP "$SMPROGRAMS\$MUI_TEMP"
  startMenuDeleteLoop:
	ClearErrors
    RMDir $MUI_TEMP
    GetFullPathName $MUI_TEMP "$MUI_TEMP\.."
    IfErrors startMenuDeleteLoopDone
    StrCmp $MUI_TEMP $SMPROGRAMS startMenuDeleteLoopDone startMenuDeleteLoop
  
  startMenuDeleteLoopDone:
  
  DeleteRegKey /ifempty HKCU "Software\Kinovea"
  
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Kinovea"

SectionEnd


;--------------------------------
;Uninstaller Functions

Function un.onInit
  !insertmacro MUI_UNGETLANGUAGE
FunctionEnd