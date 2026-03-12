// LogMyDay Scanner & QR Code interop
// Depends on: html5-qrcode.min.js, qrcode-generator.js (loaded in App.razor)

window.LogMyDayScanner = {
    _scanner: null,

    start: function (elementId, dotnetRef) {
        var self = this;

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

        function onScanSuccess(decodedText, decodedResult) {
            // Guard: dotNetRef may be disposed if user navigated away
            dotnetRef.invokeMethodAsync('OnScanSuccess', decodedText, decodedResult.result.format.formatName)
                .catch(function () { /* component was disposed, ignore */ });
        }

        function doStart() {
            self._scanner = new Html5Qrcode(elementId);

            return self._scanner.start(
                { facingMode: "environment" },
                config,
                onScanSuccess,
                function () { /* ignore per-frame scan errors */ }
            ).catch(function () {
                // Back camera failed — try front camera
                return self._scanner.start(
                    { facingMode: "user" },
                    config,
                    onScanSuccess,
                    function () { }
                );
            });
        }

        // If a previous scanner instance exists, stop it cleanly first
        if (self._scanner) {
            var old = self._scanner;
            self._scanner = null;

            return old.stop()
                .catch(function () { return Promise.resolve(); })
                .then(function () {
                    try { old.clear(); } catch (e) { }
                    return doStart();
                });
        }

        return doStart();
    },

    stop: function () {
        var self = this;

        if (self._scanner) {
            var old = self._scanner;
            self._scanner = null;

            return old.stop()
                .then(function () {
                    try { old.clear(); } catch (e) { }
                })
                .catch(function () { });
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
