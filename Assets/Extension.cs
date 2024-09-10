using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Drawing;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Antilatency.DeviceNetwork;
using Antilatency.HardwareExtensionInterface;
using Antilatency.HardwareExtensionInterface.Interop;
using TMPro;

public class Extension : MonoBehaviour
{
    public TMP_Text Text;
    public TMP_Text ErrorNote;
    public TMP_Text SideText;

    IEnumerator Fade()
    {
        using var deviceNetworkLibrary = Antilatency.DeviceNetwork.Library.load();
        Debug.Log($"Antilatency Device Network version: {deviceNetworkLibrary.getVersion()}");

        // Create a device network filter and then create a network using that filter.
        var networkFilter = deviceNetworkLibrary.createFilter();
        networkFilter.addUsbDevice(Antilatency.DeviceNetwork.Constants.AllUsbDevices);
        using var network = deviceNetworkLibrary.createNetwork(networkFilter);

        using var aheiLib = Antilatency.HardwareExtensionInterface.Library.load();
        Debug.Log($"Ahei version: {aheiLib.getVersion()}");

        using var cotaskConstructor = aheiLib.getCotaskConstructor();

        var targetNode = Antilatency.DeviceNetwork.NodeHandle.Null;

        while (targetNode == Antilatency.DeviceNetwork.NodeHandle.Null)
        {
            var supportedNodes = cotaskConstructor.findSupportedNodes(network);
            foreach (var node in supportedNodes)
            {
                if (network.nodeGetStringProperty(node, "Tag") == "ExBoard")
                {
                    targetNode = node;
                    break;
                }
            }
            ErrorNote.SetText($"Invalid Node or Tag! Connect the proper device.");
            //Thread.Sleep(100);
            Debug.Log($"Nodes count {supportedNodes.Length}");
            Thread.Sleep(100);
            yield return null;
        }

        IOPins conf;
        var cotask = cotaskConstructor.startTask(network, targetNode);

        var side = SideCheck(cotask);

        conf = Config(side);
        SideText.SetText("Side:" + side.ToString());
        cotask.Dispose();

        while (true)
        {
            cotask = cotaskConstructor.startTask(network, targetNode);
            Debug.Log("Side:" + side);
            ErrorNote.ClearMesh();
            yield return Run(cotask, conf);

            targetNode = Antilatency.DeviceNetwork.NodeHandle.Null;

            while (targetNode == Antilatency.DeviceNetwork.NodeHandle.Null)
            {
                var supportedNodes = cotaskConstructor.findSupportedNodes(network);
                foreach (var node in supportedNodes)
                {
                    if (network.nodeGetStringProperty(node, "Tag") == "ExBoard")
                    {
                        targetNode = node;
                        break;
                    }
                }
                ErrorNote.SetText($"Invalid Note! Connect the proper device.");
                Thread.Sleep(100);

                yield return null;
            }

            yield return null;
        }
    }

    public enum Sides
    {
        TOP,
        BOTTOM,
        NoConnection,
        ShortCircuit
    }

    struct IOPins
    {
        public Pins H_AXIS;
        public Pins V_AXIS;
        public Pins STATUS1;
        public Pins STATUS2;
        public Pins FUNC1;
        public Pins FUNC2;
        public Pins CLICK;
    }

    static Sides SideCheck(Antilatency.HardwareExtensionInterface.ICotask cotask)
    {
        using var button1 = cotask.createInputPin(Pins.IO1);
        using var button2 = cotask.createInputPin(Pins.IO6);

        cotask.run();
        Thread.Sleep(100);
        var state1 = button1.getState();
        var state2 = button2.getState();
        Console.WriteLine($"{state1} {state2}");

        if (state1 == PinState.Low && state2 == PinState.Low)
        {
            Console.WriteLine("ShortCircuit");
            return Sides.ShortCircuit;
        }
        if (state1 == PinState.Low && state2 == PinState.High)
        {
            Console.WriteLine("Side: TOP");
            return Sides.TOP;
        }
        if (state1 == PinState.High && state2 == PinState.Low)
        {
            Console.WriteLine("Side: BOTTOM");
            return Sides.BOTTOM;
        }
        Console.WriteLine("No Connection");
        return Sides.NoConnection;
    }

    static IOPins Config(Sides side)
    {
        var iOPins = new IOPins();
        switch (side)
        {
            case Sides.TOP:
                iOPins.STATUS1 = Pins.IO6;
                iOPins.STATUS2 = Pins.IO1;
                iOPins.FUNC1 = Pins.IO5;
                iOPins.FUNC2 = Pins.IO2;
                iOPins.H_AXIS = Pins.IOA4;
                iOPins.V_AXIS = Pins.IOA3;
                iOPins.CLICK = Pins.IO7;
                break;
            case Sides.BOTTOM:
                iOPins.STATUS1 = Pins.IO1;
                iOPins.STATUS2 = Pins.IO6;
                iOPins.FUNC1 = Pins.IO2;
                iOPins.FUNC2 = Pins.IO5;
                iOPins.H_AXIS = Pins.IOA3;
                iOPins.V_AXIS = Pins.IOA4;
                iOPins.CLICK = Pins.IO8;
                break;
            case Sides.NoConnection: break;
            case Sides.ShortCircuit: break;
        }
        return iOPins;
    }

    IEnumerator Run(Antilatency.HardwareExtensionInterface.ICotask cotask, IOPins conf)
    {
        using var ledRed = cotask.createPwmPin(conf.STATUS1, 1000, 0f);
        using var ledGreen = cotask.createOutputPin(conf.STATUS2, PinState.High);
        using var hAxis = cotask.createAnalogPin(conf.H_AXIS, 10);
        using var vAxis = cotask.createAnalogPin(conf.V_AXIS, 10);
        using var func1 = cotask.createInputPin(conf.FUNC1);
        using var func2 = cotask.createInputPin(conf.FUNC2);
        using var click = cotask.createInputPin(conf.CLICK);

        cotask.run();

        while (!cotask.isTaskFinished())
        {
            Text.SetText($"hAxis: {hAxis.getValue():f2}  vAxis {vAxis.getValue():f2}  func1: {func1.getState(),-5} func2: {func2.getState(),-5} click: {click.getState(),-5}");

            ledRed.setDuty(hAxis.getValue() * 0.4f);

            if (vAxis.getValue() >= 2)
            {
                ledGreen.setState(PinState.High);
            }
            else ledGreen.setState(PinState.Low);

            yield return new WaitForSeconds(0.01f);
        }
        cotask.Dispose();
    }

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Fade());
    }

    // Update is called once per frame
    void Update()
    {
    }
}