# Esiur

Esiur is a distributed object and resource library that facilitates real-time property modification, asynchronous function invocation, and event handling across different languages, primarily in C#, JavaScript, and Dart. It's designed for flexible, scalable, and efficient communication between client and server, suitable for applications that need rapid, bidirectional data updates and complex data handling.

## Key Features
### Real-Time Property Modification:

Allows resources to update properties in real-time across distributed environments, ensuring that changes on one side are immediately reflected on the other.

### Asynchronous Function Invocation:

Supports async instance and static functions that are triggered across distributed nodes, allowing non-blocking operations and efficient resource management.


### Event Handling:

Built-in event system enables objects to raise events and propagate them across the network, making it easy to subscribe to or respond to specific events as they happen.


### Wide Range of Data Types:

Esiur supports extensive data types for transmission and representation, including primitive types, lists, maps, enums, tuples and nullable types.

The library allows complex data structures to be transferred seamlessly, ensuring consistency and compatibility across different systems.

### Inheritance and Generic Types:

Enables inheritance, so you can define base types and extend them, enhancing code reuse and flexibility.

Supports generics, allowing for strong type definitions and collections, ensuring data integrity and reducing errors.

### Self-Describing API:

Esiur provides a self-describing API that allows the client to introspect services and resources, discovering properties, methods, and events at runtime. This feature enables dynamic and versatile usage patterns, as the client can adjust based on available functionalities without precompiled knowledge of them.

### Multi-Language Support
Esiur has implementations in C#, JavaScript, and Dart, making it highly versatile for different platforms, including desktop, web, and mobile environments. This cross-platform compatibility enables developers to use it in various client-server and P2P distributed systems where real-time data handling is essential.

## Example Use Cases
* IoT Networks: Seamlessly update and control device properties across a network, with real-time monitoring and updates.
* Game Development: Enable multiplayer synchronization of game states and events, ensuring real-time feedback for interactive experiences.
* Financial Services: Support real-time, distributed data sharing for trading platforms or monitoring systems where latency is critical.

Esiur’s robust, feature-rich approach allows developers to focus on building applications without worrying about the complexities of distributed systems, while its self-describing API and broad data type support enable flexible and scalable application design.

## Installation
- Nuget
```Install-Package Esiur```
- Command-line
``` dotnet add package Esiur ```

## Getting Started
Esiur for C# uses source generator feature of .Net framework to implement the necessary calls for property modification, which means a class must be marked as "partial" so the library automatically creates setters and getters for every property exported to the public.

>***MyResource.cs***
>```C#
>[Resource]
>public partial class HelloResource {
>    // Esiur will generate a property with name `Counts` 
>    [Export] int counts; 
>    [Export] public string SayHi(string msg) {
>        Counts++;
>        GreetingReceived?.Invoke(msg);
>        return $"Welcome, current time {DateTime.Now}";
>    }
>    [Export] public ResourceEventHandler<string> GreetingReceived;
>}
>```

### Setting up the server
Esiur resources are arrange by stores (IStore) and accessed in a similar *nix file system paths, each store is responsible for storing and retriving of it's resources from memory, files or database.

In this example we're going to use the built-in MemoryStore, which keeps its resources in RAM.

```C#
    // Warehouse is the singleton instance that holds all stores and active resources. 
    await Warehouse.Put("sys", new MemoryStore());
```

Now we can add our resource to the memory store using ***Warehouse.Put*** 

```C#
    await Warehouse.Put("sys/hello", new HelloResource());
```

To distribute our resource using Esiur IIP Protocol we need to add a DistributedServer


```C#
    await Warehouse.Put("sys/server", new DistributedServer());
```

Finally we call ***Warehouse.Open*** to initialize the system.

```C#
await Warehouse.Open();
```

To sum up

>***Program.cs***
>```C#
>    await Warehouse.Put("sys", new MemoryStore());
>    await Warehouse.Put("sys/hello", new HelloResource());
>    await Warehouse.Put("sys/server", new DistributedServer());
>    await Warehouse.Open();
>```


### Setting up the client
To access our resource remotely, we need to use it's full path including the protocol, host and instance link.

```C#
    dynamic res = await Warehouse.Get<IResource>("iip://localhost/sys/hello");
```

Now we can invoke the exported functions and read/write properties;

```C#
    var reply = await res.SayHi("Hi, I'm calling you from dotnet");
    Console.WriteLine(reply);
    Console.WriteLine($"Number of people said hi {res.Counts}");
```

Summing up

>***Program.cs***
>```C#
>    using Esiur.Resource;
>    
>    dynamic res = await Warehouse.Get<IResource>("iip://localhost/sys/hello");
>    
>    var reply = await res.SayHi("Hi, I'm calling you from dotnet");
>    
>    Console.WriteLine(reply);
>    Console.WriteLine($"Number of people said hi {res.Counts}");
>
>```

## Getting Types

In the above client example, we relied on Esiur support for dynamic objects, but this way the developer would need to know the functions, properties and events available given to them as API docs or inspect it in debugging mode. 

Esiur has a self describing feature which comes with every language it supports, allowing the developer to fetch and generate classes that match the ones on the other side (i.e. server).

After installing the Esiur nuget package a new command is added to Visual Studio Package Console Manager that is called ***Get-Template***, which generates client side classes for robust static typing.

```ps 
Get-Template iip://localhost/sys/hello
```

This will generate and add wrappers for all types needed by our resource.

Allowing us to use
```C#
    var res = await Warehouse.Get<MyResource>("iip://localhost/sys/hello");
    var reply = await res.SayHi("Static typing is better");
    Console.WriteLine(reply);
```

