﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared.Interfaces
{
    /// <summary>
    /// Provides a simple way to check whether calling code is being run by
    /// Robust.Client, or Robust.Server. Useful for code in Content.Shared
    /// that wants different behavior depending on if client or server is using it.
    /// </summary>
    public interface IModuleManager
    {
        /// <summary>
        /// Returns true if the code is being run by Robust.Client, returns false otherwise.
        /// </summary>
        bool IsClientModule();
        /// <summary>
        /// Returns true if the code is being run by Robust.Server, returns false otherwise.
        /// </summary>
        bool IsServerModule();
    }
}
