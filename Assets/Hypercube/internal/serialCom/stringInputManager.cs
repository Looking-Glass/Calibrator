using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//a utility to get (large) string data from the serial port without it puking out mid frame.

namespace hypercube
{
    public class stringInputManager : streamedInputManager
    {
        Queue<string> messages = new Queue<string>();

        public stringInputManager(SerialController _serial) : base(_serial, new byte[] { 13, 10}, 80000) //In ASCII encoding, \n is the Newline character (0x10), \r is the Carriage Return character (0x13).
        {

        }

        public string readMessage()
        {
            if (messages.Count == 0)
                return null;
            return messages.Dequeue();
        }

        protected override void processData(byte[] dataChunk)
        {
            if (dataChunk.Length == 0)
                return;

            string dataToStr = System.Text.Encoding.ASCII.GetString(dataChunk);
            messages.Enqueue(dataToStr);
        }


        public override void update(bool debug)
        {
            string data = serial.ReadSerialMessage();
            while (data != null && data != "")
            {
                if (debug)
                    Debug.Log("touchScreenInputMgr: " + data);

                byte[] b = System.Text.Encoding.Unicode.GetBytes(data);
                addData(b);
                data = serial.ReadSerialMessage();
            }

        }


    }
}
