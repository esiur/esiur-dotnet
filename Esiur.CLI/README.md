# Esiur CLI

A command-line utility to generate 

# Installation
- Command-line
``` dotnet tool install -g Esiur.CLI ```

# Usage
```
Available commands:
        get-template        Get a template from an IIP link.
        version             Print Esiur version.

Global options:
        -u, --username      Authentication username.
        -p, --password      Authentication password.
        -d, --dir           Directory name where the generated models will be saved.
        -a, --async-setters Use asynchronous property setters.

```

## Example

```
    dotnet run esiur get-template iip://localhost/sys/service
```

 
