# AutoCCX
Contain lightly threaded apps to a single CCX on AMD Ryzen CPUs for Windows 1809 (LTSC)

It is a simple demo project to use Win32 APIs to set process affinity automatically so the foreground application binds to a single CCX when the CPU load falls within a single CCX core count. The application compiles to a command line exe, runs in command line and prints out status about current foreground application, it's CPU load and it's affinity mask. It is supposed to make lightly threaded applications (i.e. old apps, games) run better on Ryzen systems on Windows 10 LTSC 2019 build 1809, which does not have CCX aware thread scheduler.

It does not have persistent affect in your system. It only temporarily adjust thread affinity on foreground application. In worst case, exit the program and restart your foreground application will turn everything back to default state.

It detects Zen, Zen+, Zen 2 and Zen 3 family of processors up to 16 cores and automatically find the CCX count and cores per CCX and tries to allocate foreground application to CCX1, unless it is given a command line argument [n] where n = perferred CCX index (starts from 1). For Example:
 > AutoCCX 2
 
Will start the program and bind lightly threaded foreground application to CCX2.

It then dynamically measures the CPU load of the application in 500ms interval. When the load exceeds 95% of the cores in one CCX (without counting hyper-threaded cores) it automatically expands the affinity to all cores. So when the application requires highly threaded load it will be granted without user intervention.

Please try it in Windows 10 1809 or earlier and report back on whether it helps to achieve better performance for lightly threaded applications.

Supported processors:
Ryzen 1000, 2000, 3000, 4000G/GE, 5000 family processors with at least 6 cores/12 threads

Tested on Ryzen 5 3600, Ryzen 7 3700X, Ryzen 9 3900X, Ryzen 9 3950X, Ryzen 9 5900X and Ryzen 9 5950X.

Those processors are NOT supported since they only have a single CCX in the package:
Ryzen 5 5600/5600X/5600G, Ryzen 7 5800X/5700G