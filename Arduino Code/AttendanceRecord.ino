/*
 Name:		AttendanceRecord.ino
 Created:	4/1/2016 12:14:41 PM
 Author:	Siddhesh Nachane (NIIT University)
*/

// Summary :
//      This sketch interacts with the PC(NUBiometricAttendanceApp) when connected as well as
//      records attendance when in Portable Mode (working on Battery)
//      Terminologies :
//          1. Fingerprint Module (Arduino Nano + GT-511C3) => FPM
//          2. Fingerprint Template (Data of 504 bytes) => FPT
//          3. Fingerprint Sensor (GT-511C3) => FPS

//  Technical Specs:
//      Communication Baudrate with PC : 9600bps	(Hardware Serial)
//		Communication Baudrate with FPS : 9600bps	(Software Serial)

#include <FPS_GT511C3.h>
#include <SoftwareSerial.h>
#include <eeprom.h>

//  Initiates FPS using Sofware Serial
// Parameters :
//		Arduino RX (GT TX) Pin
//		Arduino TX (GT RX) Pin
FPS_GT511C3 fps(2, 3);

// Setting Pin Numbers
const int buttonPin = 7;
const int greenLEDPin = 4;
const int redLEDPin = 6;

// Communication Codes
const byte DETECT_PORT[2] = { 0x55, 0xAA };

const byte ATTENDANCE_REQ = 0x11;				//PC->FPM
const byte TRANSFERING_ATTENDANCE = 0X12;		//FPM->PC
const byte NO_ATTENDANCE_TO_TRANSFER = 0x13;	//FPM->PC

const byte FPT_TRANSFER_REQ = 0x21;				//PC->FPM
const byte START_FPT_TRANSFER = 0x22;			//FPM->PC
const byte FPT_TRANSFER_FAILED = 0X23;			//FPM->PC

const byte REGISTER_REQ = 0x30;					//PC->FPM
const byte PRESS_FINGER = 0x31;					//FPM->PC
const byte REMOVE_FINGER = 0x32;				//FPM->PC

const byte ENROLL_SUCCESS = 0x41;				//FPM->PC
const byte ENROLL_FAILED = 0x42;				//FPM->PC

const byte FPS_ERR = 0x65;						//FPM->PC
const byte ERROR = 0x66;						//FPM->PC


int buttonState = 0;
int nextSavePosition = 1;
int readvalue = 0;
int prevID = 0;

void setup()
{	
	pinMode(greenLEDPin, OUTPUT);
	pinMode(redLEDPin, OUTPUT);
	Serial.begin(9600);
	delay(100);
	readvalue = EEPROM.read(0);
	if (readvalue == 255)				// Default = 255 when nothing is stored in EEPROM
		nextSavePosition = 1;
	else
		nextSavePosition = readvalue;
	prevID = EEPROM.read(nextSavePosition - 1);
}

byte rx_data = 0x00;

// Sets the FPT to Location = id of FPS
// Location and FPT transfered by PC
// Return : 
//		Success => No return, blinks GREEN LED after each Template Transfer
//		Failure => Responds transfer failed to PC, blinks RED LED Once
 void transferFPT(int id)
{
	fps.Open();

	if (fps.SetTemplate(id))
	{
		Serial.write(START_FPT_TRANSFER);
		if (fps.TransferTemplate())
		{
			digitalWrite(greenLEDPin, HIGH);
			delay(300);
			digitalWrite(greenLEDPin, LOW);
		}
		else
		{
			digitalWrite(redLEDPin, HIGH);
			delay(300);
			digitalWrite(redLEDPin, LOW);
			Serial.write(FPT_TRANSFER_FAILED);
		}
	}
	else
	{
		digitalWrite(redLEDPin, HIGH);
		delay(300);
		digitalWrite(redLEDPin, LOW);
		Serial.write(FPT_TRANSFER_FAILED);
	}
	fps.Close();
	return;
}


 // Registration/Enrolment Process :
 //		Clears all Previous Data (FPT data + Attendance Data)
 //		Enrolls Faculty At FPS ID = 0
 //		Requires pressing of finger 3 Times on the FPS
 // Returns :
 //		1 -> Successful
 //		2 -> FPS Error
 //		3 -> Enrolment Failed
 int Register_Faculty()
 {
	 fps.Open();
	 if (!fps.SetLED(true))
		 return 2;

	 fps.DeleteAll();
	 EEPROM.update(0, 1);

	 fps.EnrollStart(0);

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
					 return 1;
				 }
				 else
					return 3;
			 }
			 else
				return 3;
		 }
		 else
			return 3;
	 }
	 else
		return 3;
 }

void loop()
{
	if (Serial.available())
	{
		rx_data = Serial.read();
		delay(90);

		// Respond with string "NUBiometric" if Detect Port Request Recieved
		if (rx_data == DETECT_PORT[0])
		{
			rx_data = Serial.read();
			if (rx_data == DETECT_PORT[1])
				Serial.println("NUBiometric");
		}

		// Start Registration Process on request from PC
		else if (rx_data == REGISTER_REQ)
		{
			delay(50);
			rx_data = Serial.read();
			int response = Register_Faculty();
			if (response == 1)
				Serial.write(ENROLL_SUCCESS);
			else if (response == 2)
				Serial.write(FPS_ERR);
			else if (response == 3)
				Serial.write(ENROLL_FAILED);

			fps.SetLED(false);
			fps.Close();
		}

		// Transfers IDs stored in EEPROM to PC 
		else if (rx_data == ATTENDANCE_REQ)
		{
			delay(50);
			rx_data = Serial.read();

			int attendanceCount = nextSavePosition - 1;
			byte studentID = 0x00;

			if (attendanceCount > 0)
			{
				Serial.write(TRANSFERING_ATTENDANCE);
				Serial.write((byte)attendanceCount);
				for (int i = 1 ; i < nextSavePosition ; i++)
				{
					studentID = EEPROM.read(i);
					Serial.write(studentID);
					delay(10);
				}
				EEPROM.update(0, 1);
			}
			else
				Serial.write(NO_ATTENDANCE_TO_TRANSFER);
		}

		// Inititate FPT tranfer on Request from PC
		// Usually begins at ID = 1
		// ID = 0 is for Faculty
		else if (rx_data == FPT_TRANSFER_REQ)
		{
			delay(50);
			rx_data = Serial.read();
			transferFPT((int)rx_data);
		}
	}
	
	buttonState = digitalRead(buttonPin);
	
	// Records attendance after button is Pressed
	//		Identifies the Fingerprint and returns an Integer ID
	//		ID stored in EEPROM
	if (buttonState == HIGH){
		fps.Open();
		fps.SetLED(true);

		while (fps.IsPressFinger() == false) delay(100);
		
		fps.CaptureFinger(false);
		int id = fps.Identify1_N();
		fps.SetLED(false);
		fps.Close();
		if (id < 200)
		{
			if (id != prevID)
			{
				EEPROM.update(nextSavePosition++, id);
				EEPROM.update(0, nextSavePosition);
			}
			prevID = id;
			digitalWrite(greenLEDPin, HIGH);
			delay(500);
			digitalWrite(greenLEDPin, LOW);
		}
		else
		{
			digitalWrite(redLEDPin, HIGH);
			delay(500);
			digitalWrite(redLEDPin, LOW);
		}
		delay(100);
	}
}