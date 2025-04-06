using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Collections.Generic;
public class Calculator : MonoBehaviour
{
    public string display;
    public TMP_Text output;
    public float value;
    public float ans;
	public List<string> lines = new List<string>();
	public List<bool> numbers = new List<bool>();
	private void Start()
	{
		display = output.text;
	}
	
	public void btnPress(CalculatorBtn btn)
	{
		
	}
	public void evaluate()
	{

	}

}
