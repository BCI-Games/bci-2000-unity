/////////////////////////////////////////////////////////////////////////////
/// This file is a part of BCI2000RemoteNET, a library
/// for controlling BCI2000 <http://bci2000.org> from .NET programs.
///
///
///
/// BCI20000RemoteNET is free software: you can redistribute it
/// and/or modify it under the terms of the GNU General Public License
/// as published by the Free Software Foundation, either version 3 of
/// the License, or (at your option) any later version.
///
/// This program is distributed in the hope that it will be useful,
/// but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
/// GNU General Public License for more details.
/// 
/// You should have received a copy of the GNU General Public License
/// along with this program.  If not, see <http://www.gnu.org/licenses/>.
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace BCI2000
{
    /// <summary>
    /// Provides functionality for control of BCI2000.
    /// </summary>
    public class BCI2000RemoteProxy: BCI2000Connection
	{
		/// <summary>
		/// BCI2000 Operator states of operation, as documented on the 
		/// <see href="https://www.bci2000.org/mediawiki/index.php/User_Reference:Operator_Module_Scripting#WAIT_FOR_%3Csystem_state%3E_[%3Ctimeout_seconds%3E]">BCI2000 Wiki</see>
		/// </summary>
		public enum SystemState {
			Idle,
			Startup,
			Initialization,
			Connected,
			Resting,
			Suspended,
			ParamsModified,
			Running,
			Termination,
			Busy
		}
		
		//Subset of system states relevant to this class. Used to make sure that certain commands are valid.
		private enum RemoteState {
			Disconnected,
			Idle,
			ModulesConnected,
			Configured
		}
		private RemoteState _remoteState = RemoteState.Disconnected;


        protected override void OnOperatorConnected()
        => _remoteState = RemoteState.Idle;

        /// <summary>
        /// Starts up the specified BCI2000 modules. 
        /// </summary>
        /// <param name="modules">The modules to start.
        /// A dictionary whose keys are the names of the modules to start ("SignalGenerator", "DummyApplication", etc.), and whose values are a list of arguments to the modules ("LogKeyboard=1", "LogEyetrackerTobiiPro=1".
        /// The "--" in front of each argument is optional. Pass a null instead of a parameter list for no parameters.</param>
        public void StartupModules(IDictionary<string, IEnumerable<string>?> modules) {
			Execute("startup system");

			foreach((string name, var arguments) in modules) {
				string argumentString = ModuleConfiguration.BuildArgumentString(name, arguments);
				Execute($"start executable {name} {argumentString}");
			}

			WaitForSystemState(new[] {SystemState.Connected, SystemState.Initialization});
			_remoteState = RemoteState.ModulesConnected;

			try {
				OnModulesConnected();
			} catch (Exception e) {
				UnityEngine.Debug.LogException(e);
			}
		}
		
		/// <summary>
		/// Outputs a message to the system log.
		/// </summary>
		/// <param name="message"> The message to output to the system log </param>
		public void Log(string message)
		=> Execute($"log {message}");

		/// <summary>
		/// Outputs a warning message to the system log
		/// </summary>
		/// <param name="message"> The warning message to output to the system log </param>
		public void Warn(string message)
		=> Execute($"warn {message}");

		/// <summary>
		/// Outputs an error message to the system log
		/// </summary>
		/// <param name="message"> The error message to output to the system log </param>
		public void Error(string message)
		=> Execute($"error {message}");

		/// <summary>
		/// Waits for the system to be in the specified state.
		/// This will block until the system is in the specified state.
		/// </summary>
		/// <param name="timeout">
		/// The timeout value (in seconds) that the command will wait before failing.
		/// Leave as null to wait indefinitely.</param>
		/// <returns>True if the system state was reached within the timeout time.</returns>
		public bool WaitForSystemState(SystemState state, double? timeout = null)
		=> WaitForSystemState(state.ToString(), timeout);

		/// <summary>
		/// Waits for the system to be in one of the specified states.
		/// This will block until the system is in the specified state.
		/// </summary>
		/// <param name="timeout">The timeout value (in seconds) that the command will wait before failing.
		/// Leave as null to wait indefinitely.</param>
		/// <returns>True if one of the states was reached within the timeout time.</returns>
		public bool WaitForSystemState(SystemState[] states, double? timeout = null)
		=> WaitForSystemState(string.Join('|', states), timeout);

		private bool WaitForSystemState(string states, double? timeout = null) {
			LogIfDebugging($"Wait for {states}");
			if (timeout != null) {
				return Execute<bool>($"wait for {states} {timeout?.ToString() ?? ""}");
			} else {
				Execute($"wait for {states}");
				return true;
			}
		}

		/// <summary>
		/// Gets the current system state
		/// <exception cref="BCI2000CommandException">
		/// If response cannot be parsed into a valid system state
		/// </exception>
		/// </summary>
		public SystemState GetSystemState() {
			string response = Execute<string>("get system state");

			if (Enum.TryParse(response, out SystemState r_state))
				return r_state;
			else {
				throw new BCI2000CommandException(
					"Could not parse response into state type,"
					+ $" received response \"{response}\""
				);
			}
		}

		/// <summary>
		/// Sets BCI2000 config, readying it to run. Past this point no parameter changes can be made.
		/// </summary>
		public void SetConfig() {
			Execute("set config");
			WaitForSystemState(SystemState.Resting);
			_remoteState = RemoteState.Configured;
			OnConfigured();
		}

		/// <summary>
		/// Starts a BCI2000 run, setting config if necessary
		/// </summary>
		/// <exception cref="BCI2000CommandException">Thrown if BCI2000 is not in a state in which it can be immediately started or set config.</exception>
		public void StartRun(){
			SystemState currentState = GetSystemState();
			ThrowCommandExceptionIf(
				"Could not start BCI2000 run as it is already running.",
				currentState == SystemState.Running
			);
			if (currentState is SystemState.Connected or SystemState.Initialization)
				SetConfig();
			else {
				ThrowCommandExceptionIf(
					"Could not start BCI2000 as it is not in a valid state."
					+ $" BCI2000's state is currently {currentState}",
					currentState is not (SystemState.Resting or SystemState.Suspended or SystemState.ParamsModified)
				);
			}
			
			Execute("start system");
		}

		/// <summary> 
		/// Stops a BCI2000 run.
		/// </summary>
		/// <exception cref="BCI2000CommandException">Thrown if BCI2000 is not currently recording</exception>
		public void StopRun() {
			SystemState currentState = GetSystemState();
			ThrowCommandExceptionIf(
				"Could not stop BCI2000 run because it is not running,"
				+ $" BCI2000 currently in system state {currentState}",
				currentState != SystemState.Running
			);

			Execute("stop system");
		}

		/// <summary>
		/// Adds a parameter to BCI2000. Must be called before <see cref="StartupModules(IDictionary{string, IEnumerable{string}?})"/>.
		/// BCI2000RemoteNET provides no abstraction over BCI2000 parameters.
		/// It treats them as strings, and declares them within BCI2000 as the dynamic variant type.
		/// </summary>
		/// <param name="section">The section of the parameter. This will be the page on which the parameter appears in the BCI2000 parameters menu.</param>
		/// <param name="name">The name of the parameter.</param>
		/// <param name="defaultValue">The default value of the parameter. This argument is optional.</param>
		/// <param name="maximumValue">The maximum value of the parameter. This argument is optional.</param>
		/// <param name="minimumValue">The minimum value of the parameter. This argument is optional.</param>
		/// <exception cref="BCI2000CommandException">Thrown if BCI2000 is in an invalid state for adding parameters</exception>
		public void AddParameter(
			string section, string name, string defaultValue = "%",
			string minimumValue = "%", string maximumValue = "%"
		) {
			ThrowExceptionUnlessIdle();
			ParameterDefinition.Validate(section, name, defaultValue, minimumValue, maximumValue);

			Execute($"add parameter {section} variant {name}= {defaultValue} {minimumValue} {maximumValue}");
		}
		
		/// <summary>
		/// Loads the specified <c>.prm</c> file.
		/// If <paramref name="filename"/> is relative, it is relative to the working directory of BCI2000, which will most likely be the <c>prog</c> directory in the BCI2000 directory.
		/// Must be called before <see cref="StartupModules()"/>.
		/// </summary>
		/// <param name="filename">Path to the parameter file to load</param>
		/// <exception cref="BCI2000CommandException">Thrown if BCI2000 is in an invalid state for loading parameters</exception>
		public void LoadParameters(string filename) {
			ThrowExceptionUnlessInState(
				"Parameters must be added immediately after modules have started."
				+ " This method must be called before SetConfig() and after StartupModule()."
				+ $" Expected state Connected but got state {GetSystemState()}",
				RemoteState.ModulesConnected
			);

			Execute($"load parameters {filename}");
		}

		/// <summary>
		/// Sets a BCI2000 parameter. This must be called while the operator is in the Idle or Connected states.
		/// </summary>
		/// <param name="name">The name of the parameter to set</param>
		/// <param name="value">The value to set the parameter to</param>
		/// <exception cref="BCI2000CommandException">Thrown if BCI2000 is in an invalid state for setting parameters</exception>
		public void SetParameter(string name, string value) {
			ThrowExceptionUnlessInState(
				"Cannot set parameter, system is not in correct state."
				+ " Operator must be in state Idle or Connected,"
				+ $" but was instead in state {GetSystemState()}.",
				RemoteState.Idle, RemoteState.ModulesConnected
			);

			Execute($"set parameter {name} \"{value}\"");
		}

		/// <summary>
		/// Gets the value of a BCI2000 parameter.
		/// </summary>
		public string GetParameter(string name)
		=> GetParameter<string>(name);
		public T GetParameter<T>(string name)
		=> Execute<T>($"Get parameter {name}");

		/// <summary>
		/// Adds a state variable to BCI2000. State variables have a temporal resolution of one block.
		///	To log values with a higher temporal resolution, use <see cref="AddEvent"/>.
		/// Must be called when BCI2000 is in the Idle system state.
		/// </summary>
		/// <param name="name"> The name of the state to be added</param>
		/// <param name="bitWidth">The bit width of the new state. Must be between 1 and 32.</param>
		/// <param name="initialValue">The initial value of the state.</param>
		/// <exception cref="BCI2000CommandException">Thrown if BCI2000 is in invalid state or invalid parameters passed</param>
		public void AddState(string name, int bitWidth, uint initialValue = 0) {
			ThrowExceptionUnlessIdle();
			StateDefinition.Validate(name, bitWidth);

			Execute($"add state {name} {bitWidth} {initialValue}"); 
		}

		/// <summary>
		/// Sets the specified state to the specified value 
		/// </summary>
		/// <param name="name">The name of the state to set</param>
		/// <param name="value">The value of the state to set</param>
		public void SetState(string name, uint value)
		=> Execute($"set state {name} {value}");

		/// <summary>
		/// Gets the value of the specified state
		/// </summary>
		/// <param name="name">The name of the state to get</param>
		public uint GetState(string name)
		=> Execute<uint>($"get state {name}");

		/// <summary>
		/// Adds an event to BCI2000. Events are similar to state variables but with a temporal resolution of one sample.
		/// Must be called when BCI2000 is in the Idle system state.
		/// </summary>
		/// <param name="name"> The name of the state to be added</param>
		/// <param name="bitWidth">The bit width of the new state. Must be between 1 and 32.</param>
		/// <param name="initialValue">The initial value of the state.</param>
		/// <exception cref="BCI2000CommandException">Thrown if BCI2000 is in invalid state or invalid parameters passed</param>
		public void AddEvent(string name, int bitWidth, uint initialValue = 0) {
			ThrowExceptionUnlessIdle();
			EventDefinition.Validate(name, bitWidth);

			Execute($"add event {name} {bitWidth} {initialValue}");
		}

		/// <summary>
		/// Sets the specified event to the specified value.
		/// To set an event for a single sample duration, use <see cref="PulseEvent(string, uint)"/>
		/// </summary>
		/// <param name="name">The name of the event to set</param>
		/// <param name="value">The value of the event to set</param>
		public void SetEvent(string name, uint value)
		=> Execute($"set event {name} {value}");

		/// <summary>
		/// Sets the specified event to the specified value for a single sample duration.
		/// To set an event to a persistent value, use <see cref="SetEvent(string, uint)"/>
		/// </summary>
		/// <param name="name">The name of the event to pulse</param>
		/// <param name="value">The value of the event to pulse</param>
		public void PulseEvent(string name, uint value)
		=> Execute($"pulse event {name} {value}");

		/// <summary>
		/// Gets the value of the signal at the specified <paramref name="channel"/> and <paramref name="element"/>
		/// </summary>
		/// <param name="channel">The channel of the signal to get</param>
		/// <param name="element">The element of the signal to get</param>
		public double GetSignal(int channel, int element)
		=> Execute<double>($"get signal({channel},{element})");

		/// <summary>
		/// Gets the value of the specified event
		/// </summary>
		/// <param name="name">The name of the event to get </param>
		/// <param name="sample">The 1-indexed position in the state vector (individual event value per sample in block) </param>
		public uint GetEvent(string name, int sample)
		=> Execute<uint>($"get event[{sample}] {name}");

		/// <summary>
		/// Gets the value of the specified event
		/// </summary>
		/// <param name="name">The name of the event to get </param>
		public uint GetEvent(string name)
		=> Execute<uint>($"get event {name}");

		/// <summary>
		/// Visualizes a BCI2000 value, for example, an event.
		/// </summary>
		/// <param name="value">The expression to visualize.
		/// For example, if you wish to visualize an event called `event` pass in the value `"event"`</param>
		public void Visualize(string value) {
			ThrowExceptionIfInState(
				"Cannot visualize value before initialization."
				+ " Call this method after StartupModules()",
				RemoteState.Idle
			);

			Execute($"visualize watch {value}");
		}

		
		protected virtual void OnModulesConnected() {}
		protected virtual void OnConfigured() {}


		private void ThrowExceptionUnlessIdle()
		=> ThrowExceptionUnlessInState(
			"Operator must be in Idle state to configure,"
			+ $" but is in state {GetSystemState()}."
			+ " This method must be called before SetConfig()",
			RemoteState.Idle
		);

		private void ThrowExceptionUnlessInState(
			string exceptionMessage, params RemoteState[] validStates
		) => ThrowCommandExceptionIf(exceptionMessage, !validStates.Contains(_remoteState));
		private void ThrowExceptionIfInState(
			string exceptionMessage, params RemoteState[] invalidStates
		) => ThrowCommandExceptionIf(exceptionMessage, invalidStates.Contains(_remoteState));

		private void ThrowCommandExceptionIf(string exceptionMessage, bool condition) {
			if (condition) {
				throw new BCI2000CommandException(exceptionMessage);
			}
		}
    }
}