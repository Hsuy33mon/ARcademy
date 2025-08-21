using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class HandDataReceiver : MonoBehaviour
{
    public int port = 5065;
    public float dataTimeout = 0.2f;
    public bool is_pinching; 
    
    [Header("Debug Data")]
    public Vector2 pointerPosition;
    public bool inActivationZone;
    public bool inGoBackZone;
    public float lastUpdateTime;

    private UdpClient client;
    private Thread receiveThread;
    private bool running;
    private string latestJson;

    void Start() => StartReceiver();

    void Update()
    {
        if (!string.IsNullOrEmpty(latestJson))
        {
            try
            {
                var data = JsonUtility.FromJson<HandData>(latestJson);
                pointerPosition = new Vector2(data.pointer_position.x, data.pointer_position.y);
                inActivationZone = data.in_activation;
                inGoBackZone = data.in_goback;
                lastUpdateTime = Time.time;
                latestJson = null;
                is_pinching = data.is_pinching;

            }
            catch (Exception e) { Debug.LogWarning($"Parse error: {e.Message}"); }
        }
    }

    public bool IsDataValid => Time.time - lastUpdateTime < dataTimeout;

    void StartReceiver()
    {
        running = true;
        receiveThread = new Thread(() =>
        {
            using (client = new UdpClient(port))
            {
                while (running)
                {
                    try
                    {
                        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, port);
                        byte[] data = client.Receive(ref anyIP);
                        latestJson = Encoding.UTF8.GetString(data);
                    }
                    catch (Exception e) { Debug.LogWarning(e.Message); }
                }
            }
        });
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void OnDisable()
    {
        running = false;
        client?.Close();
    }

    [System.Serializable]
    private class HandPosition { public float x; public float y; }
    
    [System.Serializable]
    private class HandData
    {
        public bool hand_detected;
        public HandPosition pointer_position;
        public bool in_activation;
        public bool in_goback;
        public float timestamp;
        public bool is_pinching;
    }
}
