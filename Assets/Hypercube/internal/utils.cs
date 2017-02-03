using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace hypercube
{
    public class utils
    {
        public static bool getConfigPath(string relativePathToConfig)
        {
            string temp;
            return getConfigPath(relativePathToConfig, out temp);
        }
        //this method is used to figure out which drive is the usb flash drive attached to Volume, and then returns that path so that our settings can load normally from there.
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        public static bool getConfigPath(string relativePathToConfig, out string fullPath)
        {
            relativePathToConfig = relativePathToConfig.Replace('\\', Path.DirectorySeparatorChar);
            relativePathToConfig = relativePathToConfig.Replace('/', Path.DirectorySeparatorChar);

            string[] drives = System.Environment.GetLogicalDrives();
            foreach (string drive in drives)
            {
                if (File.Exists(drive + relativePathToConfig))
                {
                    fullPath = drive + relativePathToConfig;
                    return true;
                }
            }
            fullPath = Path.GetFileName(relativePathToConfig); //return the base name of the file only.
            return false;
        }
#else  //osx,  TODO: linux untested in standalone
        public static bool getConfigPath(string relativePathToConfig, out string fullPath)        
        {                        
            string[] directories = Directory.GetDirectories("/Volumes/");
            foreach (string d in directories)
            {
                string fixedPath = d + "/" + relativePathToConfig;
                fixedPath = fixedPath.Replace('\\', Path.DirectorySeparatorChar);
                fixedPath = fixedPath.Replace('/', Path.DirectorySeparatorChar);

                FileInfo f = new FileInfo (fixedPath);
                if (f.Exists)                
                {                    
                    fullPath = f.FullName;      
                    return true;  
                }            
            }            
            fullPath = Path.GetFileName(relativePathToConfig); //return the base name of the file only.        
            return false;
        }
#endif

        // Note that Color32 and Color implictly convert to each other. You may pass a Color object to this method without first casting it.
        public static string colorToHex(Color32 color)
        {
            string hex = color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");
            return hex;
        }
        public static Color hexToColor(string hex)
        {
            hex = hex.Replace("0x", "");//in case the string is formatted 0xFFFFFF
            hex = hex.Replace("#", "");//in case the string is formatted #FFFFFF
            byte a = 255;//assume fully visible unless specified in hex
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            //Only use alpha if the string has enough characters
            if (hex.Length == 8)
            {
                a = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            }
            return new Color32(r, g, b, a);
        }

        public static void vert2Bin(Vector2[,,] d, out string base64binary)
        {
            byte[] data = vert2Bin(d);
            base64binary = System.Convert.ToBase64String(data);
        }

        //this converts castMesh vertex offsets, into a binary form.
        public static byte[] vert2Bin(Vector2[,,] d)
        {
            List<byte> outData = new List<byte>();

            int slices = d.GetLength(0);
            int ax = d.GetLength(1);
            int ay = d.GetLength(2);
            outData.AddRange(System.BitConverter.GetBytes(slices)); //header
            outData.AddRange(System.BitConverter.GetBytes(ax));
            outData.AddRange(System.BitConverter.GetBytes(ay));

            for (int s = 0; s < slices; s++)  //data
            {
                for (int y = 0; y < ay; y++)
                {
                    for (int x = 0; x < ax; x++)
                    {
                        float vx = d[s, x, y].x; //pass it to a variable, because it doesn't encode properly when its directly sent into get bytes.
                        float vy = d[s, x, y].y;
                        outData.AddRange(System.BitConverter.GetBytes(vx));
                        outData.AddRange(System.BitConverter.GetBytes(vy));
                    }
                }
            }
            return outData.ToArray();
        }

        //converts stored binary calibration data into a proper array for the castMesh
        public static bool bin2Vert(string base64binary, out Vector2[,,] outData)
        {
            byte[] rawBytes = System.Convert.FromBase64String(base64binary);
            //byte[] rawBytes = System.Text.Encoding.Unicode.GetBytes(inBinaryData); //use this line if hte string contains the raw data itself
            return bin2Vert(rawBytes, out outData);
        }
        public static bool bin2Vert(byte[] rawBytes, out Vector2[,,] outData)
        {

            int sliceCount = System.BitConverter.ToInt32(rawBytes, 0);
            int xArticulation = System.BitConverter.ToInt32(rawBytes, 4);
            int yArticulation = System.BitConverter.ToInt32(rawBytes, 8);

            if (rawBytes.Length % 4 != 0)
            {
                Debug.LogWarning("The data received for the slices is malformed.");
                outData = null;
                return false;
            }

            int count = rawBytes.Length / 4;
            if (count - 3 != sliceCount * xArticulation * yArticulation * 2) //the count -3 = don't count the header info   ... the *2 = x and also y
            {
                Debug.LogWarning("The data received for the slices does not match the expected array length.");
                outData = null;
                return false;
            }

            outData = new Vector2[sliceCount, xArticulation, yArticulation];

            for (int s = 0; s < sliceCount; s++)
            {
                for (int y = 0; y < yArticulation; y++)
                {
                    for (int x = 0; x < xArticulation; x++)
                    {
                        int i = (s * xArticulation * yArticulation * 2) + (y * xArticulation * 2) + (x * 2);
                        i *= 4;
                        i += 12;//start at the end of the header.

                        outData[s, x, y] = new Vector2();
                        outData[s, x, y].x = System.BitConverter.ToSingle(rawBytes, i);
                        outData[s, x, y].y = System.BitConverter.ToSingle(rawBytes, i + 4);
                    }
                }
            }

            return true;
        }

    }

}
