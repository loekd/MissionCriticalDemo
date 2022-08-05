# Mission Critical App Demo

This solution demonstrates a mission-critical application that uses Blazor and Dapr.

![overview](/Media/overview.png "overview")

## Front-End

The front-end is a Blazor Application that can connect to the back-end API using REST calls. It will also open a SignalR connection to the Dispatch API.

## Dispatch API

The back-end is an ASP.NET 6 Web API named 'Dispatch API'. It uses a database and a message queue.
It will send and receive messages to and from the Plant API. It can communicate directly with the Front-end by using SignalR.

## Plant API

Messages sent by the Dispatch API are processed here in the Plant API. After completion, a new message is sent back to the API.
