// LogMyDay Scanner & QR Code interop
// Depends on: html5-qrcode.min.js, qrcode-generator.js (loaded in App.razor)

window.LogMyDayScanner = {
    _scanner: null,

    start: function (elementId, dotnetRef) {
        if (this._scanner) {
            this._scanner.clear();
        }

        this._scanner = new Html5Qrcode(elementId);

        var config = {
            fps: 10,
            qrbox: { width: 250, height: 250 },
            aspectRatio: 1.0,
            formatsToSupport: [
                Html5QrcodeSupportedFormats.QR_CODE,
                Html5QrcodeSupportedFormats.EAN_13,
                Html5QrcodeSupportedFormats.EAN_8,
                Html5QrcodeSupportedFormats.CODE_128,
                Html5QrcodeSupportedFormats.CODE_39,
                Html5QrcodeSupportedFormats.UPC_A,
                Html5QrcodeSupportedFormats.UPC_E,
                Html5QrcodeSupportedFormats.ITF
            ]
        };

        return this._scanner.start(
            { facingMode: "environment" },
            config,
            function (decodedText, decodedResult) {
                dotnetRef.invokeMethodAsync('OnScanSuccess', decodedText, decodedResult.result.format.formatName);
            },
            function (errorMessage) {
                // Ignore scan errors (no code in frame)
            }
        ).catch(function (err) {
            // Camera start failed — try user-facing camera
            return window.LogMyDayScanner._scanner.start(
                { facingMode: "user" },
                config,
                function (decodedText, decodedResult) {
                    dotnetRef.invokeMethodAsync('OnScanSuccess', decodedText, decodedResult.result.format.formatName);
                },
                function (errorMessage) { }
            );
        });
    },

    stop: function () {
        if (this._scanner) {
            return this._scanner.stop().then(function () {
                window.LogMyDayScanner._scanner.clear();
                window.LogMyDayScanner._scanner = null;
            }).catch(function () {
                window.LogMyDayScanner._scanner = null;
            });
        }

        return Promise.resolve();
    }
};

window.LogMyDayQrGen = {
    generate: function (elementId, text, cellSize) {
        var el = document.getElementById(elementId);
        if (!el) return;

        cellSize = cellSize || 4;
        var qr = qrcode(0, 'M');
        qr.addData(text);
        qr.make();

        el.innerHTML = qr.createSvgTag(cellSize, 0);
    },

    getDataUrl: function (text, cellSize) {
        cellSize = cellSize || 8;
        var qr = qrcode(0, 'M');
        qr.addData(text);
        qr.make();

        var moduleCount = qr.getModuleCount();
        var size = moduleCount * cellSize;
        var canvas = document.createElement('canvas');
        canvas.width = size;
        canvas.height = size;
        var ctx = canvas.getContext('2d');

        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, size, size);
        ctx.fillStyle = '#000000';

        for (var row = 0; row < moduleCount; row++) {
            for (var col = 0; col < moduleCount; col++) {
                if (qr.isDark(row, col)) {
                    ctx.fillRect(col * cellSize, row * cellSize, cellSize, cellSize);
                }
            }
        }

        return canvas.toDataURL('image/png');
    },

    downloadPng: function (text, cellSize, fileName) {
        var dataUrl = this.getDataUrl(text, cellSize);
        var a = document.createElement('a');
        a.href = dataUrl;
        a.download = fileName || 'qrcode.png';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    },

    printWithLabel: function (text, cellSize, label) {
        var dataUrl = this.getDataUrl(text, cellSize);
        var w = window.open('', '_blank', 'width=400,height=500');
        if (!w) return;

        var doc = w.document;
        doc.open();
        doc.write('<!DOCTYPE html><html><head><title>QR Code</title>');
        doc.write('<style>body{text-align:center;font-family:sans-serif;padding:2rem}img{max-width:300px}h2{margin-bottom:0.5rem}</style>');
        doc.write('</head><body>');

        var h2 = doc.createElement('h2');
        h2.textContent = label || '';
        doc.body.appendChild(h2);

        var img = doc.createElement('img');
        img.src = dataUrl;
        doc.body.appendChild(img);

        doc.close();
        w.onload = function () { w.print(); };
    }
};
