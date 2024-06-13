async function init() {
    try {

        connection = await wh.get(`iip://${window.location.hostname}`, {
            autoReconnect: true
        });

        window.demo = await connection.get("sys/demo");

        await app.setData(connection);
        console.log(connection);

        let label = document.getElementById("label");

        demo.on(":Label", () => {
            const range = document.createRange();
            const selection = window.getSelection();
            range.setStart(label, label.childNodes.length);
            range.collapse(true);
            selection.removeAllRanges();
            selection.addRange(range);
        });

        let canvas = document.getElementById("canvas");
        let ctx = canvas.getContext("2d");

        let drawing = false;

        let colors = [
            '#ffffff',
            '#000000',
            '#DB2828',
            '#21BA45',
            '#FBBD08',
            '#B5CC18',
            '#F2711C',
            '#00B5AD',
            '#2185D0',
            '#6435C9',
            '#A333C8',
            '#E03997',
        ];

        let colorId = 1;


        canvas.addEventListener("mousedown", function (e) {
            drawing = true;
            colorId = document.querySelector('input[name="color"]:checked').value;
        }, false);

        canvas.addEventListener("touchstart", (e) => {
            drawing = true;
            colorId = document.querySelector('input[name="color"]:checked').value;
        });

        canvas.addEventListener("mouseup", function (e) {
            drawing = false;
        }, false);

        canvas.addEventListener("touchend", function (e) {
            drawing = false;
        }, false);


        canvas.addEventListener("mousemove", function (e) {
 
            var rect = canvas.getBoundingClientRect();

            let x = e.clientX - rect.left;
            let y = e.clientY - rect.top;

            if (drawing) {
                ctx.fillStyle = colors[colorId];

                ctx.beginPath();
                ctx.arc(x, y, 4, 0, 2 * Math.PI);
                ctx.fill();
                demo.Draw(x / 4, y / 4, colorId);
            }
        });

        canvas.addEventListener("touchmove", function (e) {

            var rect = canvas.getBoundingClientRect();

            let x = e.touches[0].clientX - rect.left;
            let y = e.touches[0].clientY - rect.top;

            if (drawing) {
                ctx.fillStyle = colors[colorId];

                ctx.beginPath();
                ctx.arc(x, y, 4, 0, 2 * Math.PI);
                ctx.fill();
                demo.Draw(x / 4, y / 4, colorId);
            }
        });


        demo.on("Drawn", (pt) => {
            ctx.fillStyle = colors[pt.Color];
            ctx.beginPath();
            ctx.arc(pt.X * 4, pt.Y * 4, 8, 0, 2 * Math.PI);
            ctx.fill();
        });

        demo.on("Cleared", () => {
            ctx.clearRect(0, 0, canvas.width, canvas.height);
        });

        for (var x = 0; x < demo.Points.length; x++)
            for (var y = 0; y < demo.Points[x].length; y++) {
                ctx.fillStyle = colors[demo.Points[x][y]];
                ctx.beginPath();
                ctx.arc(x * 4, y * 4, 8, 0, 2 * Math.PI);
                ctx.fill();
            }
     }
    catch (ex)
    {
        alert(ex);
    }
}


const FORMAT_CONNECTION_STATUS = (x) => ["Offline", "Connecting...", "Online"][x];

 