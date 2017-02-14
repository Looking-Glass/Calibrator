using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//a utility to get (large) string data from the serial port without it puking out mid frame.
//the data is received as such:
//byte packetNum
//byte dataSize
//data
//if the size of the data doesn't match or the packet number is missed, it will ask for a resend.
//if the packet size seems good, we will send an ACK.  
//The ACK is sent as such:
//ACK<packetNum>
//The resend is sent as:
//RSD<packetNum>

//every 256 packets, the packet num resets to 0.
//MAKE SURE TO CALL expectNewMessage() EACH TIME YOU WANT THIS CLASS TO RECEIVE A STRING


namespace hypercube
{
    public class stringInputManager : streamedInputManager
    {
        
        Queue<string> messages = new Queue<string>();


        byte nextExpectedPacket = 0;

        public stringInputManager(SerialController _serial) : base(_serial, new byte[] { 13, 10}, 80000) //In ASCII encoding, \n is the Newline character (0x10), \r is the Carriage Return character (0x13).
        {
            nextExpectedPacket = 0;
        }

        public void sendSerialMessage(string request)
        {
            nextExpectedPacket = 0;
            serial.SendSerialMessage(request);
        }

        //splits the message into chunks, and then sends it incrementally to the pcb.
        //_lastSentLargeMessageSize allows us to ensure that the proper info was sent.
        public int _lastSentLargeMessageSize {get; private set;}
        public IEnumerator sendLargeSerialMessage(string message, int packetSize)
        {
            _lastSentLargeMessageSize = message.Length;
            string[] packets = splitString(message, packetSize);
            for (int i = 0; i < packets.Length; i++)
            {
                yield return new WaitForSeconds(.1f);
                serial.SendSerialMessage(packets[i]);
            }
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

            byte packetNum = dataChunk[0];
            if (packetNum != nextExpectedPacket)
            {
                //we received an unexpected packet num!  send a request to send again the correct one!
                serial.SendSerialMessage("RSD" + nextExpectedPacket);
                return;
            }

            byte packetSize = dataChunk[1];
            if (dataChunk.Length - 2 != packetSize)
            {
                //the packet has an unexpected size!  send a request to send it again.
                serial.SendSerialMessage("RSD" + nextExpectedPacket);
                return;
            }

            //everything seems ok, send an ack to get the next message
            serial.SendSerialMessage("ACK" + nextExpectedPacket);

            string dataToStr = System.Text.Encoding.ASCII.GetString(getSubArray(dataChunk, 2, packetSize));
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

        public static byte[] getSubArray(byte[] data, int index, int length)
        {
            byte[] result = new byte[length];
            System.Array.Copy(data, index, result, 0, length);
            return result;
        }

        public static string[] splitString(string s, int length)
        {
            System.Globalization.StringInfo str = new System.Globalization.StringInfo(s);

            int lengthAbs = Mathf.Abs(length);

            if (str == null || str.LengthInTextElements == 0 || lengthAbs == 0 || str.LengthInTextElements <= lengthAbs)
                return new string[] { str.ToString() };

            string[] array = new string[(str.LengthInTextElements % lengthAbs == 0 ? str.LengthInTextElements / lengthAbs: (str.LengthInTextElements / lengthAbs) + 1)];

            if (length > 0)
                for (int iStr = 0, iArray = 0; iStr < str.LengthInTextElements && iArray < array.Length; iStr += lengthAbs, iArray++)
                    array[iArray] = str.SubstringByTextElements(iStr, (str.LengthInTextElements - iStr < lengthAbs ? str.LengthInTextElements - iStr : lengthAbs));
            else // if (length < 0)
                for (int iStr = str.LengthInTextElements - 1, iArray = array.Length - 1; iStr >= 0 && iArray >= 0; iStr -= lengthAbs, iArray--)
                    array[iArray] = str.SubstringByTextElements((iStr - lengthAbs < 0 ? 0 : iStr - lengthAbs + 1), (iStr - lengthAbs < 0 ? iStr + 1 : lengthAbs));

            return array;
        }


    }
}
