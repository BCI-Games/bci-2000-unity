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
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using UnityEngine;

#nullable enable

namespace BCI2000
{
    /// <summary>
    ///Provides basic functionality for connection and communication with the BCI2000 operator module.
    /// </summary>
    public abstract class BCI2000Connection: MonoBehaviour
	{
		//Size of the input read buffer. Should be larger than the largest possible response from BCI2000.
		private const int ReadBufferSize = 2048;
		//Character indicating the end of a response
		private const char Prompt = '>';

		/// <summary>
		/// Timeout value (in milliseconds) of connection to BCI2000
		/// </summary>
		public int Timeout = 1000;

		/// <summary>
		/// Terminate operator when this object is deleted
		/// </summary>
		public bool TerminateOperatorOnDisconnect = true;
		
		/// <summary>
		/// Print debugging logs with each operation
		/// </summary>
		public bool PrintDebugLogs = false;

		[Header("Window Settings")]
		[SerializeField] private string _windowTitle = "";
		/// <summary>
		/// The title of the BCI2000 window
		/// </summary>
		public string WindowTitle {
			get => _windowTitle;
			set {
				_windowTitle = value;
				if (Connected())
					Execute($"set title \"{_windowTitle}\"");
			}
		}

		[SerializeField] private bool _hideWindow;
		/// <summary>
		/// Hide the BCI2000 window
		/// </summary>
		private bool HideWindow {
			get => _hideWindow;
			set {
				_hideWindow = value;
				if (Connected()) {
					string command = _hideWindow? "hide": "show";
					Execute(command + " window");
				}
			}
		}

		private TcpClient? _client;
		private NetworkStream? _clientStream;


		~BCI2000Connection() => Disconnect();

		/// <summary>
		///Disconnects from the operator. Terminates the operator if <see cref="TerminateOperatorOnDisconnect"/> is set.
		/// </summary>
		public void Disconnect() {
			if (TerminateOperatorOnDisconnect && Connected())
				Quit();
			
			if (_client != null) {
				_client.Close();
				_client = null;
			}
		}

		/// <summary>
		/// Starts an instance of the BCI2000 Operator on the local machine.
		/// </summary>
		/// <param name="operatorPath">The location of the operator binary</param>
		/// <param name="address"> The address on which the Operator will listen for input.
		/// Leave as default if you will only connect from the local system.
		/// Note on security: BCI2000Remote uses an unencrypted, unsecured telnet connection.
		/// Anyone who can access the connection can run BCI2000 shell scripts.
		/// This includes the capability to run arbitrary system shell code from the BCI2000 shell interface. 
		/// Use extreme caution when exposing BCI2000 to the open internet, that is, setting <paramref name="address"/> to a value other than the loopback address (127.0.0.1).
		/// Do not leave a connection across machines open unattended.
		/// A secure interface is planned for a future release, until then using BCI2000 to communicate between machines on different LANs (not on the same Wi-Fi, in different buildings, etc.) is not recommended.
		/// Communication between different machines on the same LAN should be safe provided that the network router does not forward the BCI2000's host machine's BCI2000 port (by default 3999, but can be set on startup.)
		/// </param>
		/// <param name="port"> The port on which the Operator will listen for input. Leave as default unless a specific port is needed.</param>
		public void StartOperator(string operatorPath, string address = "127.0.0.1", int port = 3999) {
			IPAddress parsedAddress = ParseAddress(address, port);

			TestSocketAvailability(parsedAddress, port);
			string arguments = BuildOperatorArguments(parsedAddress, port);
			
			try {
				System.Diagnostics.Process.Start(operatorPath, arguments);
				LogIfDebugging($"Started operator path {operatorPath} at {address}:{port}");
			}
			catch (Exception ex) {
				throw new BCI2000ConnectionException(
					$"Could not start operator at path {operatorPath}: {ex.ToString()}"
				);
			}
		}

		/// <summary>
		///Establishes a connection to an instance of BCI2000 running at the specified address and port.
		/// </summary>
		/// <param name="address">The IPv4 address to connect to.
		/// Note that this may not necessarily be the same as the one used in <see cref="StartOperator">StartOperator</see>, even if running BCI2000 locally.
		/// For example, if the operator was started on the local machine with address <c>0.0.0.0</c>, you would connect to it at address <c>127.0.0.1</c></param>
		/// <param name="port">The port on which BCI2000 is listening.
		/// If BCI2000 was started locally with <see cref="StartOperator">StartOperator</see>, this must be the same value.</param>
		public void Connect(string address = "127.0.0.1", int port = 3999) {
			IPAddress parsedAddress = ParseAddress(address, port);

			if (Connected()) {
				throw new BCI2000ConnectionException(
					"Connect() called while already connected."
					+ " Call Disconnect() first."
				);
			}
			if (_client != null) {
				throw new BCI2000ConnectionException(
					"Connect called while connection is not null."
					+ " This should not happen and is likely a bug."
					+ " Please report to the maintainer."
				);
			}

			try {
				_client = new();
				_client.Connect(parsedAddress, port);
				InitializeClient();
			}
			catch (Exception ex) {
				throw new BCI2000ConnectionException(
					"Could not connect to operator at"
					+ $" {parsedAddress.ToString()}:{port}, {ex.ToString()}"
				);
			}
		}

		/// <summary>
		/// Gets whether or not this BCI2000Remote instance is currently connected to the BCI2000 Operator
		/// </summary>
		/// <returns>Whether or not this object is currently connected to BCI2000</returns>
		public bool Connected()
		=> _client?.IsConnected() ?? false;

		/// <summary>
		/// Shuts down the connected BCI2000 instance
		/// </summary>
		public void Quit()
		=> Execute("Quit", expectEmptyResponse: false);

		/// <summary>
		/// Executes the given command and returns the result as type <typeparamref name="T"/>.
		/// Throws if the response cannot be parsed as <typeparamref name="T"/>.
		/// If you are trying to execute a command which does not produce output, use <see cref="Execute(string, bool)"/>.
		/// </summary>
		/// <typeparam name="T">Type of the result of the command. Must implement a Parse(string) method</typeparam> 
		/// <param name="command">The command to execute</param>
		public T Execute<T>(string command) {
			ThrowExceptionUnlessConnected();
			SendCommand(command);
			return GetResponseAs<T>();
		}

		/// <summary>
		/// Executes the given command. Will throw if a non-blank response is received from BCI2000 and
		/// <paramref name="expectEmptyResponse"/> is not set to false. 
		/// </summary>
		/// <param name="command">The command to send to BCI2000</param>
		/// <param name="expectEmptyResponse">By default, this function will throw if its command receives a non-empty response from BCI2000.
		/// This is because most BCI2000 commands which do not return a value will not send a response if they succeed.
		/// If set to false, this function will accept non-empty responses from BCI2000.
		public void Execute(string command, bool expectEmptyResponse = true) {
			ThrowExceptionUnlessConnected();
			SendCommand(command);
			if (expectEmptyResponse)
				ExpectEmptyResponse();
			else
				DiscardResponse();
		}


		private void ThrowExceptionUnlessConnected()
		{
			if (!Connected()) {
				throw new BCI2000ConnectionException(
					"No connection to BCI2000 Operator"
				);
			}
		}


		//Sends command to BCI2000
		private void SendCommand(string command) {
			LogIfDebugging("send: " + command);
			try {
				_clientStream!.Write(Encoding.ASCII.GetBytes(command + "\r\n"));
			}
			catch (Exception ex) {
				throw new BCI2000ConnectionException(
					$"Failed to send command to operator, {ex}"
				);
			}
		}


		//Gets the response from the operator and attempts to parse into the given type
		private T GetResponseAs<T>() {
			string response = ReceiveResponse();
			
			if (typeof(T) == typeof(string))
				return (T)(object)response;

			try {
				MethodInfo parseMethod = typeof(T).GetMethod("Parse", new Type[] {typeof(string)});
				if (parseMethod is not null)
					return (T)parseMethod.Invoke(null, new object[] {response});
			}
			catch (Exception ex) {
				throw new BCI2000CommandException(
					$"Failed to parse response {response} as type {nameof(T)}, {ex}"
				);
			}

			throw new BCI2000CommandException(
				$"No Parse(string) method found for type {nameof(T)}"
			);
		}

		//Receives response from operator and throws if response is not blank.
		//Used with commands which expect no response, such as setting events and parameters.
		private void ExpectEmptyResponse() { 
			string response = ReceiveResponse();
			if (!IsEmptyOrPrompt(response)) {
				throw new BCI2000CommandException(
					$"Expected empty response but received {response} instead"
				);
			}
		}

		//Receives response and discards the result.
		private void DiscardResponse()
		=> ReceiveResponse();


		private byte[] _readBuffer = new byte[ReadBufferSize];
		//Receives response from the operator. Blocks until the prompt character ('>') is received.
		private string ReceiveResponse() {
			StringBuilder response = new StringBuilder();
			long startTime = GetSystemTime();
			while (true)
			{
				if (!Connected()) {
					throw new BCI2000ConnectionException(
						"Lost connection while receiving response"
					);
				}

				long elapsedTime = GetSystemTime() - startTime;
				if (Timeout > 0 && elapsedTime > Timeout)
					throw new TimeoutException();

				if (!_clientStream!.DataAvailable)
					continue;
					
				int read = _clientStream.Read(_readBuffer, 0, _readBuffer.Length);
				if (read > 0)
				{
					string responseFragment = Encoding.ASCII.GetString(_readBuffer, 0, read);
					LogIfDebugging("fr:", responseFragment);

					if (EndsWithPrompt(responseFragment))
					{
						int fragmentEnd = responseFragment.LastIndexOf('>');
						//Don't include prompt in response
						response.Append(responseFragment.AsSpan(0, fragmentEnd));
						break;
					} else
						response.Append(responseFragment);
				}
			}
			LogIfDebugging("recv:", response.ToString());
			return response.ToString();
		}
		
		private long GetSystemTime()
		=> DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

		private bool EndsWithPrompt(string line)
		=> line.Trim().EndsWith(Prompt);

		private bool IsEmptyOrPrompt(string s) {
			if (s == null)
				return true;
			
			foreach (char c in s)
			{
				if (c > 0x20 && c != Prompt)
					return false;
			}
			return true;
		}

		
		private void TestSocketAvailability(IPAddress address, int port) {
			try {
				TcpClient testClient = new();
				testClient.Connect(address, port);
				testClient.Close();
				throw new BCI2000ConnectionException(
					$"{address}:{port} is occupied, is BCI2000 already running?"
				);
			} //Socket should not connect if BCI2000 is not already running
			catch (SocketException) { }
		}

		private string BuildOperatorArguments(IPAddress address, int port) {
			List<string> arguments = new() {
				$"--Telnet \"{address}:{port}\"",
				"--StartupIdle"
			};
			if (!string.IsNullOrEmpty(WindowTitle)) {
				arguments.Add($" --Title \"{WindowTitle}\" ");
			}
			if (HideWindow) {
				arguments.Add(" --Hide ");
			}
			
			return string.Join(' ', arguments);
		}

		private IPAddress ParseAddress(string address, int port) {
			if (port < 0 || port > 65535) {
				throw new BCI2000ConnectionException(
					$"Port number {port} is not valid"
				);
			}
				
			return IPAddress.Parse(address);
		}

		private void InitializeClient()
		{
			_client!.SendTimeout = Timeout;
			_client.ReceiveTimeout = Timeout;

			_clientStream = _client.GetStream();

			DiscardResponse(); //Throw out startup messages
			Execute("change directory $BCI2000LAUNCHDIR");
			OnOperatorConnected();
		}


		protected void LogIfDebugging(params string[] ar) {
			if (PrintDebugLogs)
				Debug.Log(string.Join(',', ar));
		}

		protected virtual void OnOperatorConnected() {}
    }
}