; ============================================================================
;  نصب‌کنندهٔ «طراح پیچ و مهره سهند» — NSIS 3 با رابط فارسی (راست‌چین خودکار)
;  کامپایل:  NSISDIR=<nsis>/usr/share/nsis makensis installer/sahand-setup.nsi
;  خروجی:    dist/SahandScrewMaker-Setup-0.3.0.exe
;  یادداشت: این نصب‌کننده روی Linux کامپایل شده؛ فرایند نصب باید روی Windows
;  واقعی تأیید شود (صداقت مهندسی — سند ۶).
; ============================================================================
Unicode true
SetCompressor /SOLID lzma

!include "MUI2.nsh"

!define APPNAME    "طراح پیچ و مهره سهند"
!define APPNAMEEN  "Sahand Screw & Nut Designer"
!define VERSION    "0.3.0"
!define ARP        "Software\Microsoft\Windows\CurrentVersion\Uninstall\SahandScrewMaker"
!define MUI_ICON   "..\src\Sahand.App\app.ico"
!define MUI_UNICON "..\src\Sahand.App\app.ico"
!define MUI_ABORTWARNING "از نصب ${APPNAME} خارج می‌شوید؟"

Name "${APPNAME} ${VERSION}"
OutFile "..\dist\SahandScrewMaker-Setup-${VERSION}.exe"
InstallDir "$PROGRAMFILES64\SahandScrewMaker"
InstallDirRegKey HKLM "Software\SahandScrewMaker" "InstallDir"
RequestExecutionLevel admin
ShowUninstDetails show

VIProductVersion "0.3.0.0"
VIAddVersionKey "ProductName"     "${APPNAMEEN}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "FileVersion"    "${VERSION}"
VIAddVersionKey "FileDescription" "نصب‌کنندهٔ ${APPNAME}"
VIAddVersionKey "LegalCopyright" "© 2026 — Sahand Screw & Nut Designer"

; --- صفحات (فارسی/RTL از Farsi.nlf) ---
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Farsi"

; ============================================================================
Section "برنامهٔ اصلی (الزامی)" SecMain
  SectionIn RO

  ; برنامهٔ روان WPF (خودکفا — بدون نیاز به نصب .NET)
  SetOutPath "$INSTDIR"
  File /r "..\publish\app-win\*.*"

  ; ابزار خط فرمان ssnd-cli (ویندوز، خودکفا)
  SetOutPath "$INSTDIR\cli"
  File /r "..\publish\cli-win\*.*"

  ; مستندات فارسی + مجوز
  SetOutPath "$INSTDIR\docs"
  File "..\docs\USER-GUIDE-FA.md"
  File "..\docs\BUILD-FA.md"
  File "..\docs\PRIVACY-FA.md"
  File "..\docs\CHANGELOG-FA.md"
  File "..\LICENSE"

  ; نمونه‌های آماده
  SetOutPath "$INSTDIR\samples"
  File "..\samples\bolt-m6x20.ssnd"
  File "..\samples\nut-m6.ssnd"
  File "..\samples\socket-m8x16.ssnd"
  File "..\samples\assembly-m6.ssnd"

  ; فهرست Start + حذف برنامه
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortCut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\SahandScrewNutDesigner.exe"
  CreateShortCut "$SMPROGRAMS\${APPNAME}\ssnd-cli (خط فرمان).lnk" "$INSTDIR\cli\ssnd-cli.exe"
  CreateShortCut "$SMPROGRAMS\${APPNAME}\راهنمای کاربر.lnk" "$INSTDIR\docs\USER-GUIDE-FA.md"
  CreateShortCut "$SMPROGRAMS\${APPNAME}\حذف ${APPNAME}.lnk" "$INSTDIR\Uninstall.exe"

  ; حذف‌کنندهٔ ویندوزی (Add/Remove Programs)
  WriteRegStr HKLM "Software\SahandScrewMaker" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${ARP}" "DisplayName"     "${APPNAME}"
  WriteRegStr HKLM "${ARP}" "DisplayVersion"  "${VERSION}"
  WriteRegStr HKLM "${ARP}" "DisplayIcon"     "$INSTDIR\SahandScrewNutDesigner.exe"
  WriteRegStr HKLM "${ARP}" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${ARP}" "Publisher"       "Sahand"
  WriteRegDWORD HKLM "${ARP}" "NoModify" 1
  WriteRegDWORD HKLM "${ARP}" "NoRepair" 1
  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "میانبر دسکتاپ" SecDesktop
  CreateShortCut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\SahandScrewNutDesigner.exe"
SectionEnd

; ============================================================================
Section "Uninstall"
  Delete "$DESKTOP\${APPNAME}.lnk"
  RMDir /r "$SMPROGRAMS\${APPNAME}"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "Software\SahandScrewMaker"
  DeleteRegKey HKLM "${ARP}"
SectionEnd
