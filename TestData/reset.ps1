cd D:\repos\jacosr\TreeBase\TestData
rm metadata.log
rmdir D:\repos\jacosr\TreeBase\TestData\Root -Recurse -Force
xcopy D:\repos\jacosr\TreeBase\TestData\Root.bak D:\repos\jacosr\TreeBase\TestData\Root /E /I /H /C /Y