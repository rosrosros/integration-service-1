﻿using System.Security.Principal;

namespace Vertica.Integration.Infrastructure.Windows
{
    internal static class WindowsUtils
	{
		public static string GetIdentityName()
		{
			WindowsIdentity identity = WindowsIdentity.GetCurrent();

			return identity.Name;
		}		 
	}
}