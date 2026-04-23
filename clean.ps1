#cleans the solution of all .NET build objects, such as bin and obj folders
Get-ChildItem .\ -Include bin, obj -Recurse | ForEach-Object { Remove-Item $_.fullname -Force -Recurse }
