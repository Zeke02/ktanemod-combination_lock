using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using UnityEngine;

[SuppressMessage("ReSharper", "UseStringInterpolation")]
[SuppressMessage("ReSharper", "LoopCanBeConvertedToQuery")]
[SuppressMessage("ReSharper", "MergeConditionalExpression")]
[SuppressMessage("ReSharper", "UseNullPropagation")]
public class CombinationLockModule : MonoBehaviour
{
	private enum Direction
	{
		Left,
		Right
	}

	private const int Max = 20;
	private const float DialIncrement = 360/20f;

	public KMSelectable LeftButton;
	public KMSelectable RightButton;
	public KMSelectable ResetButton;
	public TextMesh DialText;
	public GameObject Dial;
	public AudioClip DialClick;
	public AudioClip DialReset;
	public AudioClip Unlock;

	private IList<int> _inputCode;
	private IList<int> _code;
	private int _currentInput;
	private Direction _currentDirection;
	private bool _isActive;

	void Start()
	{
		Init();
	}

	private void Activate()
	{
		_isActive = true;
		GeneratePassCode();
		Debug.Log(string.Format("{0} {1} {2}", _code[0], _code[1], _code[2]));
	}

	private void Init()
	{
		_currentInput = 0;
		_currentDirection = Direction.Right;
		_inputCode = new List<int>(3);
		_code = new List<int>(3);
		SetupButtons();
		GetComponent<KMBombModule>().OnActivate += Activate;
	}

	void Update()
	{
		// ReSharper disable once InvertIf
		if (_isActive && Solved())
		{
			_isActive = false;
			GetComponent<KMAudio>().HandlePlaySoundAtTransform(Unlock.name, transform);
			GetComponent<KMBombModule>().HandlePass();
		}
	}

	private bool Solved()
	{
		if (_inputCode.Count < 3) return false;

		var solved = true;
		GeneratePassCode();

		for (var i = 0; i < _inputCode.Count; i++)
		{
			if (_inputCode[i] == _code[i]) continue;
			solved = false;
			break;
		}

		if (!solved) _inputCode.RemoveAt(2);

		return solved;
	}

	private void SetupButtons()
	{
		LeftButton.OnInteract += delegate
		{
			if (_currentDirection != Direction.Left)
			{
				_inputCode.Add(_currentInput);
				Debug.Log(_currentInput);
			}

			Dial.transform.Rotate(0f, DialIncrement, 0f);
			_currentInput--;

			if (_currentInput < 0)
				_currentInput = Max - 1;

			_currentDirection = Direction.Left;
			DialText.text = _currentInput.ToString();

			GetComponent<KMAudio>().HandlePlaySoundAtTransform(DialClick.name, transform);

			return false;
		};

		RightButton.OnInteract += delegate
		{
			if (_currentDirection != Direction.Right)
			{
				_inputCode.Add(_currentInput);
				Debug.Log(_currentInput);
			}

			Dial.transform.Rotate(0f, -DialIncrement, 0f);
			_currentInput++;

			if (_currentInput == Max)
				_currentInput = 0;

			if (_inputCode.Count == 2)
			{
				_inputCode.Add(_currentInput);
			}

			_currentDirection = Direction.Right;
			DialText.text = _currentInput.ToString();

			GetComponent<KMAudio>().HandlePlaySoundAtTransform(DialClick.name, transform);

			return false;
		};

		ResetButton.OnInteract += delegate
		{
			Dial.transform.Rotate(0f, DialIncrement * _currentInput, 0f);

			_currentInput = 0;
			_currentDirection = Direction.Right;
			_inputCode = new List<int>(3);
			DialText.text = _currentInput.ToString();

			GetComponent<KMAudio>().HandlePlaySoundAtTransform(DialReset.name, transform);

			return false;
		};
	}

	private void GeneratePassCode()
	{
		_code.Clear();
		var bombInfo = GetComponent<KMBombInfo>();
		var twoFactor = GetTwoFactorCodes(bombInfo);
		var batteryCount = GetNumberOfBatteries(bombInfo);
		var numberOfSolvedModules = GetNumberOfSolvedModules(bombInfo);

		var code1 = 0;
		var code2 = 0;

		if (twoFactor != null && twoFactor.Count > 0)
		{
			foreach (var twoFactorCode in twoFactor)
			{
				var twoFactorString = twoFactorCode.ToString();
				code1 += int.Parse(twoFactorString[twoFactorString.Length - 1].ToString());
				code2 += int.Parse(twoFactorString[0].ToString());
			}
		}
		else
		{
			code1 = GetLastDigitSerial(bombInfo) + numberOfSolvedModules;
			code2 = GetNumberOfModules(bombInfo);
		}

		code1 += batteryCount;
		code2 += numberOfSolvedModules;

		code1 = code1 >= Max ? code1 - Max : code1;
		code2 = code2 >= Max ? code2 - Max : code2;

		var code3 = code1 + code2;
		code3 = code3 >= Max ? code3 - Max : code3;

		_code.Add(code1);
		_code.Add(code2);
		_code.Add(code3);
	}

	private static IList<int> GetTwoFactorCodes(KMBombInfo bombInfo)
	{
		var twoFactorCodes = new List<int> {};

		if (bombInfo == null)
		{
			twoFactorCodes.Add(201928);
			twoFactorCodes.Add(501929);
		}
		else
		{
			var responses = bombInfo.QueryWidgets(TwoFactorWidget.WidgetQueryTwofactor, null);

			foreach (var response in responses)
			{
				var responseDict = JsonConvert.DeserializeObject<Dictionary<string, int>>(response);
				twoFactorCodes.Add(responseDict[TwoFactorWidget.WidgetTwofactorKey]);
			}
		}

		return twoFactorCodes;
	}

	private static int GetLastDigitSerial(KMBombInfo bombInfo)
	{
		var serial = "IE7E63";

		// ReSharper disable once InvertIf
		if (bombInfo != null)
		{
            var responses = bombInfo.QueryWidgets(KMBombInfo.QUERYKEY_GET_SERIAL_NUMBER, null);

            if (responses.Count > 0)
                serial = JsonConvert.DeserializeObject<Dictionary<string, string>>(responses[0])["serial"];
		}

		return int.Parse(serial[serial.Length - 1].ToString());
	}

	private static int GetNumberOfModules(KMBombInfo bombInfo)
	{
		return bombInfo == null ? 3 : bombInfo.GetModuleNames().Count;
	}

	private static int GetNumberOfSolvedModules(KMBombInfo bombInfo)
	{
		return bombInfo == null ? 1 : bombInfo.GetSolvedModuleNames().Count;
	}

	private static int GetNumberOfBatteries(KMBombInfo bombInfo)
	{
		if (bombInfo == null) return 1;

		var batteryCount = 0;
		var responses = bombInfo.QueryWidgets(KMBombInfo.QUERYKEY_GET_BATTERIES, null);

		foreach (var response in responses)
		{
			var responseDict = JsonConvert.DeserializeObject<Dictionary<string, int>>(response);
			batteryCount += responseDict["numbatteries"];
		}

		return batteryCount;
	}
}
