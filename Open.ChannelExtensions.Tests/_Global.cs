global using System;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Linq;
global using System.Threading;
global using System.Threading.Channels;
global using System.Threading.Tasks;
global using Xunit;

[assembly: SuppressMessage(
		"ConfigureAwait",
		"ConfigureAwaitEnforcer:ConfigureAwaitEnforcer",
		Justification = "Should not be used in test projects.")]