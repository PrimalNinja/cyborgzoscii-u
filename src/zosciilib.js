function ZOSCIILib() 
{
	var m_objThis = this;
	
	var NEGOTIATIONINSTANCES = 5;
	var CHALLENGEQUESTION = "Surely you can't be serious.";
	var CHALLENGEANSWER = "I am serious. And don't call me Shirley.";
	
	var m_objConnection = null;
	var m_strGUID = '';

	// helpers
	
	function getParameterValue(arrParameters_a, strParameter)
	{
		var strResult = '';
		
		// TODO
		
		return strResult
	}
	
	function getGUID()
	{
		return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c)
		{
			var r = Math.random() * 16 | 0,
			v = c == 'x' ? r : (r & 0x3 | 0x8);
			return v.toString(16);
		});
	}

	function isFunction(cb_a)
	{
		var getType = {};
		return fn_a && getType.toString.call(fn_a) === '[object Function]';
	}

	// ROM management
	
	function createNegotiationROMAddresses() 
	{
		var arrAddresses = [];
		var objUsed = {};
		var intTarget = 256 * NEGOTIATIONINSTANCES;

		while (arrAddresses.length < intTarget) 
		{
			var intAddr = Math.floor(Math.random() * 65536);
			if (!objUsed[intAddr]) 
			{
				objUsed[intAddr] = true;
				arrAddresses.push(intAddr);
			}
		}
		return arrAddresses;
	}

	function createNegotiationROM(arrNegotiationROMAddresses_a) 
	{
		var arrROM = [];
		for (var intI = 0; intI < 65536; intI++) 
		{
			arrROM.push(0);
		}

		var intValue = 0;
		var intInstanceCount = 0;
		for (var intJ = 0; intJ < arrNegotiationROMAddresses_a.length; intJ++) 
		{
			var intAddr = arrNegotiationROMAddresses_a[intJ];
			arrROM[intAddr] = intValue;

			intInstanceCount++;
			if (intInstanceCount >= NEGOTIATIONINSTANCES) 
			{
				intValue++;
				intInstanceCount = 0;
			}
		}

		return arrROM;
	}

	function createTemporaryROM() 
	{
		var arrROM = [];
		var objUsed = {};
		var intTarget = 256 * 255;

		while (arrROM.length < intTarget) 
		{
			var intAddr = Math.floor(Math.random() * 65536);
			if (!objUsed[intAddr]) 
			{
				objUsed[intAddr] = true;
				arrROM.push(intAddr);
			}
		}
		return arrROM;
	}

	// privates
	
	function onConnectFrom(cbOnNegotiatedConnection_a)
	{
		var arrMyAddresses = createNegotiationROMAddresses();
	}
	
	function onConnectTo(cbOnNegotiatedConnection_a, cbOnError_a)
	{
		var arrMyNegotiationAddresses = createNegotiationROMAddresses();
		var arrTheirAddresses = [];
		
		var intNONCE = 0;
		var arrNONCEs = [true, false, false, false]; // first is ourselves
		
		// first we make some addresses to send a temporary ROM
		
		var objNegotiation = {
			"function": "negotiateConnection",
			"parameters": [
				{ "name": "guid", "value": m_strGUID },
				{ "name": "nonce", "value": intNONCE },
				{ "name": "negotiation", "value": arrMyNegotiationAddresses }
			]
		}

		sendTo(objNegotiation, function(objResponse_a)
		{
			intTheirGUID = objResponse_a.guid;
			var intTheirNONCE = objResponse_a.nonce;
			arrTheirNegotiationAddresses = objResponse_a.negotiation;
			
			if ((m_strGUID === intTheirGUID) && (intNONCE === 1) && (!arrNONCEs[1]))
			{
				arrNONCEs[1] = true;
				var arrMyNegotiationROM = createNegotiationROM(arrMyNegotiationAddresses);
				var arrTheirNegotiationROM = createNegotiationROM(arrTheirNegotiationAddresses);
				var arrTemporaryROM = createTemporaryROM();

				// second we make a temporary ROM to continue negotiation
		
				intNONCE = 2;
				arrNONCEs[2] = true;
				var objNegotiation = {
					"function": "provideTemporaryROM",
					"parameters": [
						{ "name": "nonce", "value": intNONCE },
						{ "name": "negotiation", "value": arrTemporaryROM }
					]
				}

				sendTo(objNegotiation, function(objResponse_a)
				{
					intTheirNONCE = objResponse_a.nonce;

					if ((m_strGUID === intTheirGUID) && (intNONCE === 3) && (!arrNONCEs[3]))
					{
						arrNONCEs[3] = true;

						// thirdly we send a challenge question
				
						intNONCE = 4;
						arrNONCEs[5] = true;
						var objNegotiation = {
							"function": "challengeQuestion",
							"parameters": [
								{ "name": "nonce", "value": intNONCE },
								{ "name": "negotiation", "value": CHALLENGEQUESTION }
							]
						}

						sendTo(objNegotiation, function(objResponse_a)
						{
							intTheirNONCE = objResponse_a.nonce;
							var strTheirAnswer = m_objThis.decode(arrTemporaryROM, objResponse_a.negotiation);

							if ((m_strGUID === intTheirGUID) && (intNONCE === 5) && (!arrNONCEs[5]) && (strTheirAnswer === CHALLENGEANSWER))
							{
								arrNONCEs[5] = true;
								
								// we have now established secure communications
								if (isFunction(cbOnNegotiatedConnection_a))
								{
									cbOnNegotiatedConnection_a();
								}
							}
							else
							{
								if (isFunction(cbOnError_a))
								{
									cbOnError_a("Negotiation Error");
								}
							}
						});
					}
					else
					{
						if (isFunction(cbOnError_a))
						{
							cbOnError_a("Negotiation Error");
						}
					}
				});
			}
			else
			{
				if (isFunction(cbOnError_a))
				{
					cbOnError_a("Negotiation Error");
				}
			}
		});
	}
	
	// publics
	
	this.encode = function(strR_a, strM_a) 
	{
		var arrM = strM_a.split('');
		var arrResult = [];
		for (var intI = 0; intI < arrM.length; intI++) 
		{
			var strC = arrM[intI];
			var arrR = strR_a.split('');
			var arrIndices = [];
			for (var intJ = 0; intJ < arrR.length; intJ++) 
			{
				if (arrR[intJ] === strC) 
				{
					arrIndices.push(intJ);
				}
			}
			arrIndices.sort(function() { return Math.random() - 0.5; });
			arrResult.push(arrIndices[0]);
		}
		return arrResult;
	};

	this.decode = function(strR_a, arrA_a) 
	{
		var strResult = '';
		for (var intI = 0; intI < arrA_a.length; intI++) 
		{
			strResult += strR_a[arrA_a[intI]];
		}
		return strResult;
	};
	
	this.connect = function(strURL_a, intPort_a, cbOnConnect_a, cbOnNegotiatedConnection_a)
	{
		connectTo(strURL_a, intPort_a, function(objConnection_a)
		{
			m_objConnection = objConnection_a;
			m_strGUID = getGUID();

			if (isFunction(cbOnConnect_a))
			{
				cbOnConnect_a();
			}
			
			onConnectTo(cbOnNegotiatedConnection_a);
		});
	};

	this.listen = function(cbOnConnect_a, cbOnNegotiatedConnection_a)
	{
		listenFrom(strURL_a, intPort_a, function(objConnection_a)
		{
			m_objConnection = objConnection_a;
			m_strGUID = getGUID();
			
			if (isFunction(cbOnConnect_a))
			{
				cbOnConnect_a();
			}
			
			onConnectFrom(cbOnNegotiatedConnection_a);
		});
	};
	
	// actual connectivity
	
	function connectTo(strURL_a, intPort_a, cbOnConnect_a)
	{
		var objConnection = null;
		
		// connect
		
		// TODO
		
		// we have connected
		
		if (isFunction(cbOnConnect_a))
		{
			cbOnConnect_a(objConnection);
		}
	}

	function listenFrom(cbOnConnect_a)
	{
		var objConnection = null;
		
		
		// connect 
		
		// TODO
		
		// we have connected
		
		if (isFunction(cbOnConnect_a))
		{
			cbOnConnect_a(objConnection);
		}
	}
	
	function readFrom(cb_a)
	{
		// receive data from other party
		
		// TODO
	}
	
	function sendTo(arrJSON_a, cb_a)
	{
		// send data to other party
		
		// TODO
	}
}