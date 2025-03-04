using UnityEngine;
using BCI2000;

public class BasicStateWriter : MonoBehaviour
{
    public uint stateValue;
    public string stateName = "test";
    [SerializeField] private bool _visualizeStateWhenConnected = true;
    [SerializeField] private BCI2000RemoteProxy _bci2000Proxy;

    void Reset()
    {
        _bci2000Proxy = GetComponent<BCI2000RemoteProxy>();
    }

    void Start()
    {
        _bci2000Proxy ??= FindAnyObjectByType<BCI2000RemoteProxy>();
        if (_visualizeStateWhenConnected) {
            _bci2000Proxy.OperatorConnected += ()
                => _bci2000Proxy.Visualize(stateName);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_bci2000Proxy && _bci2000Proxy.Connected())
            {
                uint newValue = (stateValue + 1) % 2;

                Debug.Log($"Setting test state to new value: {newValue}");
                _bci2000Proxy.SetState(stateName, newValue);

                stateValue = _bci2000Proxy.GetState(stateName);
                Debug.Log($"Fetched test state value: {stateValue}");
            }
        }
    }
}
