# Mission Critical App Demo

This solution demonstrates a mission-critical application that uses Blazor and Dapr.

![overview](/Media/overview.png "overview")

## Front-End

The front-end is a Blazor Application that can connect to the back-end API using REST calls.

## Back-End

The back-end is an ASP.NET 6 Web API named 'Dispatch API'. It uses a database and a message queue.

## Background Processor

Messages sent by the Dispatch API are processed here. After completion, a message is sent back to the API.
