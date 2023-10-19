# Patch for Anchor Wallet

This is a fix for the Anchor bug - when wallet processes duplicate on the system and start to load the CPU.
![alt text](https://github.com/Saltant/anchor-patch/blob/master/anchor_patch.png?raw=true)

The program must be run as the current user (the user who runs Anchor Wallet). The program monitors "zombie" processes-windows of Anchor Wallet and closes them if there are any.

The program can be added to the autoloader, having previously set the parameters:


```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <Platform>Any CPU</Platform>
    <PublishDir>bin\Release\net7.0\publish\win-x86\</PublishDir>
    <PublishProtocol>FileSystem</PublishProtocol>
    <_TargetId>Folder</_TargetId>
    <TargetFramework>net7.0</TargetFramework>
    <OutputType>WinExe</OutputType>
    <RuntimeIdentifier>win-x86</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishReadyToRun>true</PublishReadyToRun>
    <PublishTrimmed>false</PublishTrimmed>
  </PropertyGroup>
</Project>
```
```<OutputType>WinExe</OutputType>``` will allow to run the program without console interface.

To place the application in Windows autoloader you need to press ```Win+R```, type: ```shell:startup``` and place a shortcut of ```AnchorPatch.exe``` file from the build folder in the opened folder.

Or you can use the installer from the [release section](https://github.com/Saltant/anchor-patch/releases/latest "Latest Releases"), it will automatically install the patch and add it to autorun.
