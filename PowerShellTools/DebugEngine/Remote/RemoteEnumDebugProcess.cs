﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Automation = System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Diagnostics;
using System.Windows.Forms;
using PowerShellTools.Common;

namespace PowerShellTools.DebugEngine.Remote
{
    /// <summary>
    /// Works with the host service to find all attachable processes on a 
    /// remote machine. Stores said processes in a list structure.
    /// </summary>
    internal class RemoteEnumDebugProcess : IEnumDebugProcesses2
    {
        private List<ScriptDebugProcess> _runningProcesses;
        private string _remoteComputer;
        private uint _currIndex;

        public RemoteEnumDebugProcess(string remoteComputer)
        {
            _runningProcesses = new List<ScriptDebugProcess>();
            _remoteComputer = remoteComputer;
            _currIndex = 0;
        }

        /// <summary>
        /// Asks HostService to find all attachable processes on the given machine. Will prompt user to retry connecting if 
        /// the call to EnumerateRemoteProcesses returns null.
        /// </summary>
        /// <param name="remotePort"></param>
        public void connect(IDebugPort2 remotePort)
        {
            // host needs to be initialized before we can connect/enumerate
            UiContextUtilities.ActivateUiContext(UiContextUtilities.CreateUiContext(Constants.PowerShellReplCreationUiContextGuid));
            if (!PowerShellToolsPackage.PowerShellHostInitialized)
            {
                // TODO: UI Work required to give user inidcation that it is waiting for debugger to get alive.
                PowerShellToolsPackage.DebuggerReadyEvent.WaitOne();
            }

            List<KeyValuePair<uint, string>> information;
            string errorMessage = string.Empty;
            while (true)
            {
                information = PowerShellToolsPackage.Debugger.DebuggingService.EnumerateRemoteProcesses(_remoteComputer, out errorMessage);

                if (information != null)
                {
                    break;
                }

                DialogResult dlgRes = MessageBox.Show(errorMessage, null, MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);
                if (dlgRes != DialogResult.Retry)
                {
                    return;
                }
            }
            
            foreach (KeyValuePair<uint, string> info in information)
            {
                _runningProcesses.Add(new ScriptDebugProcess(remotePort, info.Key, info.Value, _remoteComputer));
            }
        }

        public int Clone(out IEnumDebugProcesses2 ppEnum)
        {
            ppEnum = new RemoteEnumDebugProcess(_remoteComputer);
            foreach (ScriptDebugProcess process in _runningProcesses)
            {
                ((RemoteEnumDebugProcess)ppEnum)._runningProcesses.Add(process);
            }
            return VSConstants.S_OK;
        }

        // <summary>
        /// Gets number of processes retrieved
        /// </summary>
        /// <param name="pcelt">Out parameter for number of processes</param>
        /// <returns></returns>
        public int GetCount(out uint pcelt)
        {
            pcelt = (uint)_runningProcesses.Count();
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Fills the given array with a specified number of processes
        /// </summary>
        /// <param name="celt">How many processes to attempt to retrieve</param>
        /// <param name="rgelt">Array to fill with said processes</param>
        /// <param name="pceltFetched">How many processes were actually put in the array</param>
        /// <returns>If successful, returns S_OK. Returns S_FALSE if fewer than the requested number of elements could be returned</returns>
        public int Next(uint celt, IDebugProcess2[] rgelt, ref uint pceltFetched)
        {
            int index = 0;
            pceltFetched = 0;
            while (pceltFetched < celt)
            {
                if (_currIndex == _runningProcesses.Count())
                {
                    return VSConstants.S_FALSE;
                }
                rgelt[index++] = _runningProcesses.ElementAt((int)_currIndex++);
                pceltFetched++;
            }
            return VSConstants.S_OK;
        }

        public int Reset()
        {
            _currIndex = 0;
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Skips the given number of processes in the enumeration
        /// </summary>
        /// <param name="celt">Number to skip</param>
        /// <returns>If successful, returns S_OK. Returns S_FALSE if celt is greater than the number of remaining elements</returns>
        public int Skip(uint celt)
        {
            _currIndex += celt;
            if(_currIndex >= _runningProcesses.Count())
            {
                _currIndex = (uint)_runningProcesses.Count() - 1;
                return VSConstants.S_FALSE;
            }
            return VSConstants.S_OK;
        }
    }
}