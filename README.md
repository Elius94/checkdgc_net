# checkdgc_net
Example implementation of the .net library for greenpass verification.
Useful if you are triyng to build a triage automatic totem with camera.

[![Readme Card](https://github-readme-stats.vercel.app/api/pin/?username=elius94&repo=checkdgc_net&theme=github_dark&show_icons=true)](https://github.com/Elius94/checkdgc_net
)

## .NET 4.7 + OpenCV
It uses .NET 4.7 framework with Open CV (OpencvSharp)

## Fast connection to USB camera
It uses the standard USB camers windows driver to grab video stream.

## Automatic update of rules/blacklists/trustlist and settings.
Using the official DgcReader from @DevTrevi -> https://github.com/DevTrevi/DgcReader
That library is official and approved by the Italian government, but it should be upgraded every time a new version is released, keep watch the repo.
It stores in cache all the json data from the government.

 - Return Valid or Not Valid Splashscreen as result of checking GP.
 - Text to speech synth to say "Codice QR Valido!"
 - It Displays only the name and the date of Birth

Check: 

![image](https://user-images.githubusercontent.com/14907987/151567903-a85821ba-6cbd-4a6b-8bde-f46d70f2701f.png)

Result:

![ok](https://user-images.githubusercontent.com/14907987/151567299-54c29b7a-adac-42b4-9f81-798dc6d91a56.jpg)

Or:

![ko](https://user-images.githubusercontent.com/14907987/151567359-394be8e2-84b1-4c31-bf86-f25ab0625cc7.jpg)

The name and the date of birth will be added on the bottom of the result screen.
