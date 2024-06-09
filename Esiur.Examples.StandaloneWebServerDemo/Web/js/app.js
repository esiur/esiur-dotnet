async function init() {
    try {

        connection = await wh.get(`iip://${window.location.hostname}`, {
            autoReconnect: true
        });

        window.demo = await connection.get("sys/demo");

    }
    catch (ex)
    {
        alert(ex);
    }
}


const FORMAT_CONNECTION_STATUS = (x) => ["Offline", "Connecting...", "Online"][x];
