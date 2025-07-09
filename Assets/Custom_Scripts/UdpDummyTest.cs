using UnityEngine;
using System.Net;
using System.Net.Sockets;

public class UDPDummyTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("[UDPDummy] Start chamado!");
        UdpClient client = new UdpClient();
        client.Client.Bind(new IPEndPoint(IPAddress.Any, 9955));
        Debug.Log("[UDPDummy] UDP Bind feito!");
    }
}
