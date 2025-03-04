BCI 2000 Unity
===
Unity Package for interfacing with [BCI2000][bci2000]

Description
---
BCI2000 is a neuroscience research toolkit which simplifies the process of performing experiments. This package is based on [BCI2000RemoteNET][bci2000remoteNET], which provides functionality for controlling BCI2000 from .NET programs.

BCI2000Remote communicates with BCI2000 over tcp by sending [Operator Scripting Commands][operator scripting], receiving and processing responses.

Usage
---
1. Add [BCI 2000 Unity][package link] using git URL [through the Unity Package Manager][upm instructions]
2. Add a `ConfigurableBCI2000RemoteProxy` Component to your scene and configure it to start or connect to a local operator.
3. Get reference to the remote proxy to read or write BCI2000 states, events, and parameters.

See [the Wiki][github wiki] for more information.

Documentation of the library it is based on is located on the [BCI2000 Wiki][bci2000remoteNET wiki]


[bci2000]: https://bci2000.org
[bci2000remoteNET]: https://github.com/neurotechcenter/BCI2000RemoteNET
[operator scripting]: https://www.bci2000.org/mediawiki/index.php/User_Reference:Operator_Module_Scripting
[package link]: https://github.com/bci-games/bci-2000-unity
[upm instructions]: https://docs.unity3d.com/Manual/upm-ui-giturl
[bci2000remoteNET wiki]: https://www.bci2000.org/mediawiki/index.php/Contributions:BCI2000RemoteNET
[github wiki]: https://github.com/BCI-Games/bci-2000-unity/wiki