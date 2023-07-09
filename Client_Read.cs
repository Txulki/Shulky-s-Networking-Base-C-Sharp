using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using Networking;
using shared_handler;
using Defective.JSON;

namespace ClientLib
{
    public class Client_Read
    {
        /*========================

           MESSAGE READING CLASS

        So... Format of messages:

        IP:PORT#PLAYERID;TIMEID;HEADER;MD5 --> ALL OF THIS IS MANDATORY APART FROM THE MD5 WHICH IS ONLY FOR IMPORTANT MESSAGES
        Then we introduce a # to split into 3.

        #MSG1|MSG2|MSG3|MSG4... --> NON MANDATORY PART. ACTUAL CONTENT OF MESSAGE


        So the -t-h-r-e-e- fourt parts now because of ACKs and MD5

        ===============================================
            IP:PORT                 --> Address
            PLAYERID;TIMEID;HEADER;  --> Identifiers
            MSG1|MSG2|MSG3|MSG4...  --> Message 
            ACK_ID|MD5              --> Confirmations
        ===============================================



        =========================*/

        //RECEIVED INFO VARIABLES
        int rc_Port;
        string rc_Tag;
        int rc_TimeID;
        IPAddress rc_IP;

        //CLIENT INFO VARIABLES
        UDP_Client main;

        //CONNECTION VARIABLES
        UDP_Networking _connection;

        //UTILITIES
        Handler _handler;

        //MESSAGE INFO
        string content;
        private string[] Message;
        bool send_Confirm = false;


        /*=======================
         SETUP SETUP SETUP SETUP
        =======================*/
        public void Setup(string msg, UDP_Networking networking, Handler handler, UDP_Client master)
        {
            main = master;
            content = msg;
            _connection = networking;
            _handler = handler;
            Parse();
        }

        /*=======================
         PARSE PARSE PARSE PARSE
        =======================*/

        private void Parse()
        {
            try
            {
                // Parse --> IP:PORT, Identifier and Message
                string[] Parts = content.Split(main.separator_parts);

                // Parse --> IP:PORT
                string[] Address = Parts[0].Split(":");
                rc_IP = IPAddress.Parse(Address[0]);
                rc_Port = int.Parse(Address[1]);

                // Parse --> Identifier PLAYERID;TIMEID;HEADER
                string[] Identifier = Parts[1].Split(main.separator_identifier);
                rc_Tag = Identifier[0];
                rc_TimeID = int.Parse(Identifier[1]);
                int header = int.Parse(Identifier[2]);

                if (Parts.Length > 3)
                {
                    string[] Confirmations = Parts[3].Split(main.separator_identifier);
                    if (Confirmations[0] == "1")
                    {
                        send_Confirm = true;
                    }
                }

                if (send_Confirm)
                {
                    main.SendACK(rc_TimeID);
                }

                //CHECK TIME ID
                if (main.LastIDs[header] > rc_TimeID)
                {
                    if (header != 1)
                    {
                        return;
                    }
                }
                main.LastIDs[header] = rc_TimeID;

                // Parse --> Message
                Message = Parts[2].Split(main.separator_message);

                // Check header
                switch (header)
                {
                    case 0: //Client joining
                        ClientInfoReceived();
                        break;
                    case 1: //Confirmation received
                        ConfirmationReceived();
                        break;
                    case 3: //UPDATE FROM CLIENTS
                        ClientUpdateReceived();
                        break;
                    case 4: //Receives info of self.
                        PlayerFromServer();
                        break;
                }

            }
            catch (Exception e)
            {
                Debug.Log("Couldn't parse message:" + e);
            }
        }

        /*=======================
         CASES CASES CASES CASES
        =======================*/

        /*-------------------------
          JOIN INTRO JOIN INTRO
        -------------------------*/

        private void ClientInfoReceived()
        {
            var jsonObject = new JSONObject(Message[0]);

            string rcvTag = jsonObject[0].stringValue;
            

            foreach (Client_Instance Instance in main.server_Clients)
            {
                if (Instance.Tag == rcvTag) //No doubles please :>
                {
                    return;
                }
            }

            if(rcvTag == main.Tag)
            {
                return;
            }

            Client_Instance newInstance = new Client_Instance();
            GameObject newObject = Transform.Instantiate(main.Instance_Prefab, Vector3.zero, Quaternion.identity);
            newObject.name = rcvTag + "_" + "Instance";
            newInstance.Setup_Client(rcvTag, newObject);
            Debug.Log("Received new player info: " + rcvTag + " connected.");

            main.server_Clients.Add(newInstance);
        }

        /*-------------------------
          CONFIRMATION RECEIVED
        -------------------------*/

        private void ConfirmationReceived()
        {
            List<ConfirmationRequest> ToRemove = new List<ConfirmationRequest>();
            foreach (ConfirmationRequest Request in main.AwaitingResponses)
            {
                if (Request.TimeID == rc_TimeID)
                {
                    ToRemove.Add(Request);
                }
            }

            while (ToRemove.Count > 0)
            {
                main.AwaitingResponses.Remove(ToRemove[0]);
                ToRemove.Remove(ToRemove[0]);
            }
        }

        /*-------------------------
          CLIENT UPDATE RECEIVED
        -------------------------*/

        private void ClientUpdateReceived()
        {
            //Client_UpdatePack receivedJSON = JsonUtility.FromJson<Client_UpdatePack>(Message[0]);

            //Debug.Log("Update pack");
            var UpdatePack = new JSONObject(Message[0]);

            if (UpdatePack[0].stringValue == main.Tag)
            {
                Debug.Log("Actually this happens");
                //main.move.ReceivedPos(_handler.ExtractVector(UpdatePack[1].stringValue));
            }
            else
            {
                foreach (Client_Instance client in main.server_Clients)
                {
                    Debug.Log(UpdatePack[0].stringValue + "_" + client.Tag);
                    Debug.Log(main.server_Clients.Count);
                    if (UpdatePack[0].stringValue == client.Tag)
                    {
                        
                        client.SetPos(_handler.ExtractVector(UpdatePack[1].stringValue) , _handler.ExtractVector(UpdatePack[2].stringValue));

                        client.Instance.transform.position = client.Pos;
                    }
                }
            }
        }

        /*-------------------------
          PLAYER FROM SERVER!
        -------------------------*/

        private void PlayerFromServer() //Basically we receive a JSON with info the server should have saved from previous sessions.
        {
            var JsonPlayer = new JSONObject(Message[0]);
        }
    }
}
