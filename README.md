# TeamViewerController
[Archived Code] Program to remotely administer multiple computers easily. Can control screen, keyboard, view remote screen, lock remote computer, and run shell commands.

This program can do basic administration, however if more control is needed, it can spawn TeamViewer for remote desktop access. 

This program originally had an install process, but now is a one click install.
This program basically runs as a daemon in the background listening for requests from the remote administration client.

This is old archived code so some of the control center code is in this program, however is relatively outdated.
This code also has not been tested in years so might not work as intended.

This program should only be ran on computers owned by the user and isn't very secure. It can access computers remotely across networks by using UPnP for forwarding.
Since no encryption is used this program is a security risk if forwarded using UPnP.
