using System.IO;
using UnityEngine;

namespace BCI2000
{
    public class ConfigurableBCI2000RemoteProxy: BCI2000RemoteProxy
    {
        [Space(20)]
        [SerializeField] private OperatorStartupConfiguration _startupConfiguration;

        [Space(10)]
        [SerializeField] private bool _autoConnect = true;
        [SerializeField] private bool _startWhenConnected = true;
        [SerializeField] private bool _stopWhenDestroyed = true;

        [Header("Operator")]
        [SerializeField] private bool _autoStartLocalOperator = false;
        public string OperatorPath;
        public string OperatorAddress = "127.0.0.1";
        public int OperatorPort = 3999;


        void Awake() {
            if (_autoStartLocalOperator) {
                if (_autoConnect)
                    StartAndConnectToLocalOperator(
                        OperatorPath, OperatorPort, OperatorAddress
                    );
                else
                    StartOperator(OperatorPath, port: OperatorPort);
            } else if (_autoConnect)
                ConnectOnceAvailable(OperatorPort, OperatorAddress);
        }

        void OnDestroy() {
            if (_stopWhenDestroyed && Connected())
                StopRun();
        }


        public void StartAndConnectToLocalOperator
        (
            string operatorPath, int port = 3999,
            string address = "127.0.0.1"
        ) {
            if (!File.Exists(operatorPath)) {
                Debug.LogWarning("Operator path invalid, aborting...");
                return;
            }
            StartOperator(operatorPath, address, port);
            ConnectOnceAvailable(port);
        }


        protected override void OnOperatorConnected() {
            base.OnOperatorConnected();
            SystemState currentState = GetSystemState();

            if (currentState == SystemState.Idle) {
                _startupConfiguration.ForEachParameter(AddParameter);
                _startupConfiguration.ForEachState(AddState);
                _startupConfiguration.ForEachEvent(AddEvent);

                if(_startupConfiguration.StartModulesWithConnection)
                    StartupModules(_startupConfiguration.GetModuleDictionary());
            }
            else if (_startupConfiguration.RequiresSetup) {
                Debug.LogWarning("Failed configure as BCI2000 operator was not in idle state");
            }

            if (_startWhenConnected)
                StartRun();
        }

        protected override void OnModulesConnected()
        => _startupConfiguration.ForEachParameterFile(LoadParameterFile);


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