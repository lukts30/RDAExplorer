## Windows Runtime Requirements

- [VCRedist for Visual Studio 2022](https://aka.ms/vs/17/release/vc_redist.x64.exe)
- [.NET Runtime 6.0.1 or .NET Desktop Runtime 6.0.1](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- winfsp

### Installing WinFsp

Download and run the latest stable version of the [WinFsp .msi installer](https://github.com/billziss-gh/winfsp/releases/latest)
In the installer the default selected options are sufficient.

![](https://virtio-fs.gitlab.io/winfsp-installer.png)

You should not need to reboot unless WinFsp was already running on your system.


## USAGE AND OPTIONS

```
MntRDA version 1.0.0.0-alpha
Usage: MntRDA [options] top_rda#lower_rda#...#lowest_rda mountpoint
Overlays one or several rda files into one single mount point.
(Read-only virtual file system)
   
general options:
    -f                     Run in foreground. (Recommended) 
    -s                     Disables Fuse multithreading. 
                           Might prevent race conditions.

    -d                     Enable debug output
    -h   --help            print help
    -V   --version         print version
   
MntRDA options:
    top:...:lowest        List of rda files separated by '#' to merge. 
                          At least one rda file is required. 
```

### Example Usage

```
MntRDA -s -f data21.rda#data20.rda#data19.rda#data18.rda#data17.rda#data16.rda#data15.rda#data14.rda#data13.rda#data12.rda#data11.rda#data10.rda#data9.rda#data8.rda#data7.rda#data6.rda#data5.rda#data4.rda#data3.rda#data2.rda#data1.rda#data0.rda mountpoint
```

Windows :
>When mounting as a fixed disk drive you can either mount to an unused drive letter, or to a path representing a **non-existent subdirectory** of an existing parent directory or drive.
The instructions are from [rclone](https://rclone.org/commands/rclone_mount/#mounting-modes-on-windows) but they also apply to MntRDA. 

```
MntRDA data0.rda X:
MntRDA data1.rda#data0.rda C:\path\parent\mount
MntRDA data1.rda#data0.rda X:
```

## Known problems

- The VFS is case-sensitive