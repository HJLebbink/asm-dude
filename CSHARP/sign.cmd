rem openssl genrsa -out AsmDude.key 4096

packages\Microsoft.VSSDK.Vsixsigntool.14.1.24720\tools\vssdk\VSIXSignTool.exe sign /f AsmDude.key /p SignItToGetRidOfTheWarning ./bin/Release/AsmDude.vsix