using TMPro;
using UnityEngine;


public class CalculatorBtn : MonoBehaviour
{
    public bool op;
    public bool num;
    public bool separator;
    public int value;
    public int? operation;
    public int? sep;
    public TMP_Text txt;
    public string text;

    void Start()
    {
        txt = transform.GetChild(0).GetComponent<TMP_Text>();
        text = txt.text;
        num = int.TryParse(text, out value);
        if(num == false)
        {
            if(text == "-" || text == "+" || text == "√" || text == "÷" || text == "X" || text == "ans")
			{
                op = true;
                if(text == "X")
                {
                    operation = 0;
                }if(text == "-")
                {
                    operation = 1;
                }
				if (text == "+")
				{
                    operation = 2;

				}
				if (text == "÷")
				{
					operation = 3;

				}
				if (text == "√")
				{
                    operation = 4;

				}if (text == "ans")
				{
                    operation = 5;

				}
                

			}
            else
            {
                separator = true;
                if(text == "(")
                {
                    sep = 0;
                }if(text == ")")
                {
                    sep = 1;
                }if(text == ".")
                {
                    sep = 2;
                }
            }

		}

    }

}
