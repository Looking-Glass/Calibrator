using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class messageWindow : MonoBehaviour {

    private static messageWindow instance = null;
    //public static TextMesh _get() { return instane.text; }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(this.gameObject);
        //end singleton

    }

    void Update()
    {

        if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.X))
        {
            window.SetActive(false);
        }
    }

    public static void showMessage(string message)
    {
        if (!instance)
            return;
        instance.text.text = message;
        instance.window.SetActive(true);
    }

    public static void setVisible(bool onOff)
    {
        if (!instance)
            return;

        instance.window.SetActive(onOff);
    }

    public static bool isVisible()
    {
        if (!instance)
            return false;

        return instance.window.activeSelf;
    }

    public GameObject window;
    public UnityEngine.UI.Text text;


}
