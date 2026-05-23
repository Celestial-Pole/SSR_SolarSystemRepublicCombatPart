using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Quat4Test : MonoBehaviour
{
    public Vector4 vectorWithAngle;
    public float r;
    
    // 用于存储计算结果以便在IMGUI中显示
    private Vector3 axis;
    private Vector3 localPosition;
    private float signedAngle;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        axis = new Vector3(vectorWithAngle.x, vectorWithAngle.y, vectorWithAngle.z);
        localPosition = Quaternion.AngleAxis(vectorWithAngle.w, axis) * Vector3.forward * r;
        transform.localPosition = localPosition;
        signedAngle = Vector3.SignedAngle(Vector3.forward * r, localPosition, axis);
        
    }
    
    // OnGUI用于IMGUI绘制
    void OnGUI()
    {
        // 创建一个窗口
        GUI.Window(0, new Rect(10, 10, 320, 200), DrawDebugWindow, "Quat4Test 调试信息");
    }
    
    // 窗口绘制函数
    void DrawDebugWindow(int windowID)
    {
        GUILayout.BeginVertical();
        
        GUILayout.Label("vectorWithAngle: " + vectorWithAngle.ToString("F3"));
        GUILayout.Label("旋转轴(axis): " + axis.ToString("F3"));
        GUILayout.Label("旋转角度(angle): " + vectorWithAngle.w.ToString("F2") + "度");
        GUILayout.Label("变换后的位置: " + localPosition.ToString("F3"));
        GUILayout.Label("SignedAngle结果: " + signedAngle.ToString("F2") + "度");
        
        GUILayout.EndVertical();
        
        // 允许拖动窗口
        GUI.DragWindow();
    }
}