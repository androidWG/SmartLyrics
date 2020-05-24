var express = require("express");
var app = express(); app.listen(3000, () => {
    console.log("Server running on port 3000");
});

const Kuroshiro = require('kuroshiro');
const KuroshiroAnalyzer = require('kuroshiro-analyzer-kuromoji');

app.get("/romaji", async (request, response, next) => {
    try {
        const kuroshiro = new Kuroshiro();
        await kuroshiro.init(new KuroshiroAnalyzer({ dictPath: 'node_modules/kuromoji/dict' }));

        const text = request.query.text;
        const to = request.query.to;
        const mode = request.query.mode;

        console.log(text);

        const lines = text.split("\n")
        console.log(lines);

        const romajilines = await Promise.all(lines
            .map(async line => {
                if (Kuroshiro.Util.hasJapanese(line)){
                    return await kuroshiro.convert(line, { to: to, mode: mode });
                } else {
                    return line;
                }
            }))
            
        const romaji = romajilines.join("<br>");
        console.log(romaji);
        response.send(romaji);
    }
    catch (e) {
        next(e);
    }
});