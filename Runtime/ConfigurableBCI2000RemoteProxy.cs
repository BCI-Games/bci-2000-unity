using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using Castle.Core.Internal;

namespace BCI2000
{
    public class ConfigurableBCI2000RemoteProxy: BCI2000RemoteProxy
    {
        [SerializeField] private bool _autoConnect = true;
        [SerializeField] private bool _startWhenConnected = true;
        [SerializeField] private bool _stopWithScene = true;

        [Header("Operator")]
        public string OperatorAddress = "127.0.0.1";
        public int OperatorPort = 3999;

        [Header("Modules")]
        [SerializeField] private bool _startModulesWithConnection = true;
        [SerializeField] private ModuleConfiguration _signalSourceModule = new("SignalGenerator");
        [SerializeField] private ModuleConfiguration _signalProcessingModule = new("DummySignalProcessing");
        [SerializeField] private ModuleConfiguration _applicationModule = new("DummyApplication");
        [SerializeField] private ModuleConfiguration[] _otherModules;


        [Header("Parameters")]
        [SerializeField] private ParameterDefinition[] _parameters;
        [SerializeField] private string[] _parameterFiles;

        [Header("States")]
        [SerializeField] private StateDefinition[] _states;

        [Header("Events")]
        [SerializeField] private EventDefinition[] _events;


        void Awake()
        {
            if (_autoConnect)
                ConnectOnceAvailable(OperatorPort, OperatorAddress);
        }

        void OnDestroy()
        {
            if (_stopWithScene)
                Stop();
        }


        protected override void OnOperatorConnected()
        {
            SystemState currentState = GetSystemState();
            if (currentState == SystemState.Idle) {
                AddParameters(_parameters);
                AddStates(_states);
                AddEvents(_events);
                
                if(_startModulesWithConnection)
                    StartModules();
            }
            else if (
                _startModulesWithConnection
                || !_parameters.IsNullOrEmpty()
                || !_states.IsNullOrEmpty()
                || !_events.IsNullOrEmpty()
            ) {
                Debug.LogWarning("Failed configure as BCI2000 operator was not in idle state");
            }

            if (_startWhenConnected)
                Start();
        }

        protected override void OnModulesConnected()
        => Array.ForEach(_parameterFiles, LoadParameterFile);


        public void AddParameters(ParameterDefinition[] parameters) {
            foreach (
                var (
                    section, name, defaultValue,
                    minimumValue, maximumValue
                ) in parameters
            ) {
                AddParameter (
                    section, name, defaultValue,
                    minimumValue, maximumValue
                );
            }
        }

        public void AddStates(StateDefinition[] states) {
            foreach(var (name, bitWidth, initialValue) in states)
                AddEvent(name, bitWidth, initialValue);
        }

        public void AddEvents(EventDefinition[] events) {
            foreach(var (name, bitWidth, initialValue) in events)
                AddEvent(name, bitWidth, initialValue);
        }


        public void StartModules() {
            Dictionary<string, IEnumerable<string>> moduleDictionary = new();
            ModuleConfiguration[] moduleSet = new[] {
                _signalSourceModule, _signalProcessingModule, _applicationModule
            };

            foreach(var (name, arguments) in moduleSet.Concat(_otherModules))
                moduleDictionary.Add(name, arguments);
            
            StartupModules(moduleDictionary);
        }


        public void LoadParameterFile(string path) {
            if (File.Exists(path))
                LoadParameters(path);
            else {
                throw new BCI2000CommandException(
                    "Parameter file not found: " + path
                );
            }
        }

        public void ConnectOnceAvailable(
            int port = 3999, string address = "127.0.0.1"
        ) => this.ExecuteWhenHostAvailable(
            address, port, () => Connect(address, port)
        );
    }
}