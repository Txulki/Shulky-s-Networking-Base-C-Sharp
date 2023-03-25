using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using Networking;
using shared_handler;

namespace ServerLib
{
    public class Server_Read
    {
        /*========================

           MESSAGE READING CLASS

        So... Format of messages:

        IP:PORT#PLAYERID;TIMEID;HEADER --> ALL OF THIS IS MANDATORY APART FROM THE MD5 WHICH IS ONLY FOR IMPORTANT MESSAGES
        Then we introduce a # to split into 3.

        #MSG1|MSG2|MSG3|MSG4... --> NON MANDATORY PART. ACTUAL CONTENT OF MESSAGE


        So the three parts are

        ===============================================
            IP:PORT                 --> Address
            PLAYERID;TIMEID;HEADER  --> Identifiers
            MSG1|MSG2|MSG3|MSG4...  --> Message 
        ===============================================



        =========================*/

        int cl_Port;
        string cl_Tag;
        string cl_MsgID;
        string sv_Tag = "sv";
        IPAddress cl_IP;
        UDP_Networking _server;
        UDP_Server main;
        Handler _handler;

        //LIST OF CLIENTS CONNECTED TO SERVER
        List<Server_Client> sv_Clients;

        
        //SEPARATOR CHARACTERS
        char separator_parts = '#';
        char separator_identifier = ';';
        char separator_message = ';';


        //TIME TAG ID INT
        int cl_TimeID;

        //CLIENT STATUS
        Server_Client client;
        bool cl_Verified = false;



        //MESSAGE INFO
        string content;
        bool send_Confirm = false;

        public void Setup(string msg, UDP_Networking server, Handler handler, UDP_Server master, List<Server_Client> ClientList)
        {
            main = master;
            content = msg;
            _server = server;
            _handler = handler;

            //Assigning client list to UDP_Server's
            sv_Clients = ClientList;
            Parse();
        }

        private void Parse()
        {
            try
            {
                // Parse --> IP:PORT, Identifier and Message
                string[] Parts = content.Split(separator_parts);

                // Parse --> IP:PORT
                string[] Address = Parts[0].Split(":");
                cl_IP = IPAddress.Parse(Address[0]);
                cl_Port = int.Parse(Address[1]);

                // Parse --> Identifier PLAYERID;TIMEID;HEADER;MD5
                string[] Identifier = Parts[1].Split(separator_identifier);
                cl_Tag = Identifier[0];
                cl_TimeID = int.Parse(Identifier[1]);
                int header = int.Parse(Identifier[2]);

                if (Parts.Length > 3)
                {
                    string[] Confirmations = Parts[3].Split(separator_identifier);
                    if (Confirmations[0] == "1")
                    {
                        send_Confirm = true;
                    }
                }

                //CHECK IF THE PACKET COMES FROM A CLIENT ALREADY IN THE GAME.
                client = _handler.ClientByTag(cl_Tag);

                if (send_Confirm)
                {
                    SendACK();
                }

                if (client != null)
                {
                    cl_Verified = true;

                    if (!client.Check_ID(header, cl_TimeID))
                    {
                        return;
                    }
                }

                // Parse --> Message
                string[] Message = Parts[2].Split(separator_message);

                // Check header
                switch (header)
                {
                    case 0: //Client joining
                        client_Login();
                        break;
                    case 1: //Confirmation received
                        ConfirmationReceived();
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.Log("Couldn't parse message:" + e);
            }
        }


        /*==============================================
        
                MESSAGE SENDER

        ===============================================*/

        public void Reply(string header, bool ACK = false, params string[] parts)
        { 
            // MSG1|MSG2|MSG3|MSG4...  --> Message 
            string Message = _handler.CompileList(separator_message, parts);

            //If we want to receive an acknowledgement.
            string Identifier = null;

            main.TimeID++;
            Identifier = _handler.CompileList(separator_identifier, sv_Tag, main.TimeID.ToString(), header);

            //Joins parts together.
            string ToSend = Identifier + separator_parts + Message;

            if (ACK)
            {
                string Confirmations = _handler.CompileList(separator_identifier, "1");
                //Joins parts together.
                ToSend = ToSend + separator_parts + Confirmations;
                client.AwaitConfirmation(ToSend, main.TimeID);
            }

            _server.Send(ToSend, cl_IP, cl_Port);
            Debug.Log(ToSend);
        }

        public void SendTo(string header, Server_Client target, bool ACK = false, params string[] parts)
        {
            // MSG1|MSG2|MSG3|MSG4...  --> Message 
            string Message = _handler.CompileList(separator_message, parts);

            //If we want to receive an acknowledgement.
            string Identifier = null;

            main.TimeID++;
            Identifier = _handler.CompileList(separator_identifier, sv_Tag, main.TimeID.ToString(), header);

            //Joins parts together.
            string ToSend = Identifier + separator_parts + Message;

            if (ACK)
            {
                string Confirmations = _handler.CompileList(separator_identifier, "1");
                //Joins parts together.
                ToSend = ToSend + separator_parts + Confirmations;
                target.AwaitConfirmation(ToSend, main.TimeID);
            }
            _server.Send(ToSend, target.IP, target.Port);
        }

        /*==============================================
        
                ACK SENDER

        ===============================================*/

        public void SendACK()
        {
            string Message = _handler.CompileList(separator_message, "ACK");
            string Identifier = _handler.CompileList(separator_identifier, sv_Tag, cl_TimeID.ToString(), "1");

            /*string Confirmations = _handler.CompileList(separator_identifier, "1");*/

            string ToSend = Identifier + separator_parts + Message;

            _server.Send(ToSend, cl_IP, cl_Port);
            Debug.Log(ToSend);
        }
        

        /*=======================
         CASES CASES CASES CASES
        =======================*/

        /*-------------------------
         JOIN REQUEST JOIN REQUEST
        -------------------------*/

        private void client_Login()
        {
            if(!cl_Verified)
            {
                if(_handler.ClientByTag(cl_Tag) == null)
                {
                    //NO DUPLICATE SAME USER IN SERVER.
                    //CREATE NEW CLIENT INSTANCE FOR THIS BOI.

                    Server_Client newClient = new Server_Client();
                    newClient.Setup_Client(cl_Port, cl_IP, cl_Tag);
                    client = newClient;
                    main.server_Clients.Add(client);
                }
            }

            //NOW THE CLIENT HAS BEEN REGISTERED OR IT WAS ALREADY REGISTERED BEFORE.
            //WE HAVE TO SEND THE INFORMATION ON THE GAME STATUS NOW.

            foreach(Server_Client user in main.server_Clients)
            {
                if(user != client)
                {
                    Reply("0", true, _handler.CompileClient(user));
                    SendTo("0", user, true, _handler.CompileClient(client));
                }
            }

            //TIME TO SEND AN ACK I GUESS...



        }

        /*-------------------------
         CONFIRMATION RECEIVED
        -------------------------*/
        private void ConfirmationReceived()
        {
            if(client.AwaitingResponses.Count == 0)
            {
                return;
            }

            List<ConfirmationRequest> ToRemove = new List<ConfirmationRequest>();
            foreach (ConfirmationRequest Request in client.AwaitingResponses)
            {
                if (Request.TimeID == cl_TimeID)
                {
                    ToRemove.Add(Request);
                }
            }

            while (ToRemove.Count > 0)
            {
                client.AwaitingResponses.Remove(ToRemove[0]);
                ToRemove.Remove(ToRemove[0]);
            }
        }
    }


    /*===============================
     CLIENT INSTANCE CLIENT INSTANCE
    ===============================*/
    public class Server_Client
    {
        public int Port;
        public IPAddress IP;
        public string Tag;
        public int TimeID;

        public List<ConfirmationRequest> AwaitingResponses; //MESSAGES WAITING FOR CONFIRMATION

        public List<int> LastIDs; 

        public void Setup_Client(int port, IPAddress ip, string tag)
        {
            Port = port;
            IP = ip;
            Tag = tag;

            AwaitingResponses = new List<ConfirmationRequest>();
            LastIDs = new List<int>();
            for(int i = 0; i < 5; i++)
            {
                LastIDs.Add(-1);
            }
        }


        //FUNCTION TO COMPARE IF RECEIVED MESSAGE IS NEWER THAN LAST ONE.
        public bool Check_ID(int header, int ID)
        {
            if(LastIDs[header] < ID)
            {
                LastIDs[header] = ID;
                return true;
            }
            return false;
        }

        //FUNCTION TO REGISTER AWAITING RESPONSES.
        public void AwaitConfirmation(string ToSend, int ToSendID)
        {
            ConfirmationRequest newRequest = new ConfirmationRequest();
            newRequest.SetupRequest(ToSend, ToSendID);
            AwaitingResponses.Add(newRequest);
        }
    }
}