using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class maskmanager : MonoBehaviour
{
    //所有面具
    public List<maskData>allMasks = new List<maskData>();
    //当前面具组（卡堆）
    public List<maskData>deck = new List<maskData>();
    //当前面具
    public List<maskData>handMasks = new List<maskData>();
    //已用面具
    public List<maskData>discardedMasks = new List<maskData>();

    public int handMasksSize = 3;
    



    void Start()
    {
        InitializeMask();
    }
    void InitializeMask()
    {
      
        deck = new List<maskData>(allMasks);
        
  
    }


    void Update()
    {
        
    }
}
