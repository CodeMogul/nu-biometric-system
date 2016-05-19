/*
 Name:		Enrolment.ino
 Created:	3/31/2016 6:23:13 PM
 Author:	Siddhesh Nachane (NIIT University)
*/

// Summary :
//      This sketch interacts with the PC(NUBiometricEnrolment) when connected
//      Terminologies :
//          1. Fingerprint Module (Arduino Nano + GT-511C3) => FPM
//          2. Fingerprint Template (Data of 504 bytes) => FPT
//          3. Fingerprint Sensor (GT-511C3) => FPS

//  Technical Specs:
//      Communication Baudrate with PC : 115200bps	(Hardware Serial)
//		Communication Baudrate with FPS : 9600bps	(Software Serial)

#include <FPS_GT511C3.h>

#ifndef SOFTWARESERIAL_H
#define SOFTWARESERIAL_H
#endif

//  Initiates FPS using Sofware Serial
// Parameters :
//		Arduino RX (GT TX) Pin
//		Arduino TX (GT RX) Pin
FPS_GT511C3 fps(2, 3);

// Communication Codes
const byte DETECT_PORT[2] = { 0x55, 0xAA };		//PC->FPM
const byte START = 0x10;						//PC->FPM
const byte TEMPLATE_DATA_SENT = 0x14;			//PC->FPM

const byte PRESS_FINGER = 0x03;					//FPM->PC
const byte REMOVE_FINGER = 0x13;				//FPM->PC
const byte SUCCESS = 0x09;						//FPM->PC
const byte FAILED = 0x19;						//FPM->PC
const byte COMM_ERR = 0x29;						//FPM->PC
const byte FPS_ERR = 0x39;						//FPM->PC


void setup()
{
	Serial.begin(115200);
	delay(100);
}

//Creates a fingerprint template and returns it [498 Bytes] + 6 Bytes Header
int Enroll()
{
	fps.Open();
	if (!fps.SetLED(true))
		return 4;
	int enrollid = -1;    // Performs normal enrollment but does not store the template inside the sensor instead returns it
	fps.EnrollStart(enrollid);

	Serial.write(PRESS_FINGER);
	while (fps.IsPressFinger() == false) delay(100);
	bool bret = fps.CaptureFinger(true);
	int iret = 0;
	if (bret != false)
	{
		Serial.write(REMOVE_FINGER);
		fps.Enroll1();
		while (fps.IsPressFinger() == true) delay(100);

		Serial.write(PRESS_FINGER);
		while (fps.IsPressFinger() == false) delay(100);
		bret = fps.CaptureFinger(true);
		if (bret != false)
		{
			Serial.write(REMOVE_FINGER);
			fps.Enroll2();
			while (fps.IsPressFinger() == true) delay(100);

			Serial.write(PRESS_FINGER);
			while (fps.IsPressFinger() == false) delay(100);
			bret = fps.CaptureFinger(true);
			if (bret != false)
			{
				Serial.write(REMOVE_FINGER);
				iret = fps.Enroll3();
				if (iret == 0)
				{
					Serial.write(TEMPLATE_DATA_SENT);
					if (fps.TransferRawBytes())
						return 1;
					else
						return 2;
				}
				return 3;
			}
			return 3;
		}
		return 3;
	}
	return 3;
}

byte rx_data = 0;
int response = 0;
void loop()
{
	if (Serial.available()){
		rx_data = Serial.read();
		delay(90);
		if (rx_data == DETECT_PORT[0])
			if (Serial.read() == DETECT_PORT[1])
				Serial.println("NUBiometric");

		if (rx_data == START){
			response = Enroll();
			if (response == 1)
				Serial.write(SUCCESS);
			else if (response == 2)
				Serial.write(COMM_ERR);
			else if (response == 3)
				Serial.write(FAILED);
			else if (response == 4);
				Serial.write(FPS_ERR);
			fps.SetLED(false);
			fps.Close();
		}
	}
}