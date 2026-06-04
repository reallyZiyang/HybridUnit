using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;





public class Demo : MonoBehaviour
{

    public float it_time = 1.5f;

    public Transform[] eachTransforms;
    List<string> a = new List<string>();

    private void Start()
    {


        StartCoroutine(StartCO());

    }

    void Update()
    {
        //var list = new List<Transform>(1000);
        a.Add(Time.time.ToString());
    }


    private IEnumerator StartCO()
    {


        while (true)
        {

            if (null == eachTransforms || eachTransforms.Length <= 0)
            {
                yield break;
            }
            
            foreach (Transform child in eachTransforms)
            {

                child.gameObject.SetActive(false);


            }

            yield return null;

            foreach (Transform child in eachTransforms)
            {
                child.gameObject.SetActive(true);

            }

            yield return new WaitForSeconds(it_time);



        }
    }

}