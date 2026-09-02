const fs = require('fs');
const https = require('https');
const path = require('path');

const fixes = {
    'adana-kebap': 'https://upload.wikimedia.org/wikipedia/commons/0/05/Adana_kebap_1.jpg',
    'adana-kebap-2': 'https://upload.wikimedia.org/wikipedia/commons/0/05/Adana_kebap_1.jpg',
    'kebap': 'https://upload.wikimedia.org/wikipedia/commons/f/ff/Shish_kebab_01.jpg',
    'kebap-2': 'https://upload.wikimedia.org/wikipedia/commons/f/ff/Shish_kebab_01.jpg',
    'kebap-3': 'https://upload.wikimedia.org/wikipedia/commons/f/ff/Shish_kebab_01.jpg',
    'kebap-4': 'https://upload.wikimedia.org/wikipedia/commons/f/ff/Shish_kebab_01.jpg',
    'iskender': 'https://upload.wikimedia.org/wikipedia/commons/8/81/İskender_kebap_1.jpg',
    'iskender-2': 'https://upload.wikimedia.org/wikipedia/commons/8/81/İskender_kebap_1.jpg',
    'iskender-3': 'https://upload.wikimedia.org/wikipedia/commons/8/81/İskender_kebap_1.jpg',
    'iskender-4': 'https://upload.wikimedia.org/wikipedia/commons/8/81/İskender_kebap_1.jpg',
    'lahmacun': 'https://upload.wikimedia.org/wikipedia/commons/c/c7/Lahmacun.jpg',
    'lahmacun-2': 'https://upload.wikimedia.org/wikipedia/commons/c/c7/Lahmacun.jpg',
    'lahmacun-4': 'https://upload.wikimedia.org/wikipedia/commons/c/c7/Lahmacun.jpg',
    'manti': 'https://upload.wikimedia.org/wikipedia/commons/4/49/Manti_in_a_metal_plate.jpg',
    'manti-2': 'https://upload.wikimedia.org/wikipedia/commons/4/49/Manti_in_a_metal_plate.jpg',
    'manti-4': 'https://upload.wikimedia.org/wikipedia/commons/4/49/Manti_in_a_metal_plate.jpg',
    'pide': 'https://upload.wikimedia.org/wikipedia/commons/a/ac/Turkish_pide.jpg',
    'pide-2': 'https://upload.wikimedia.org/wikipedia/commons/a/ac/Turkish_pide.jpg',
    'kusbasili-pide': 'https://upload.wikimedia.org/wikipedia/commons/a/ac/Turkish_pide.jpg',
    'kofte': 'https://upload.wikimedia.org/wikipedia/commons/0/05/Inegol_kofte_and_piyaz.jpg',
    'kofte-2': 'https://upload.wikimedia.org/wikipedia/commons/0/05/Inegol_kofte_and_piyaz.jpg',
    'kofte-4': 'https://upload.wikimedia.org/wikipedia/commons/0/05/Inegol_kofte_and_piyaz.jpg',
    'pilav': 'https://upload.wikimedia.org/wikipedia/commons/b/b3/Arroz_blanco_%284074805726%29.jpg',
    'dolma': 'https://upload.wikimedia.org/wikipedia/commons/7/7b/Sarma_yaprak.jpg',
    'doner-durum': 'https://upload.wikimedia.org/wikipedia/commons/1/15/Shawarma_dürüm_2021.jpg',
    'doner-durum-2': 'https://upload.wikimedia.org/wikipedia/commons/1/15/Shawarma_dürüm_2021.jpg',
    'doner-plate': 'https://upload.wikimedia.org/wikipedia/commons/3/36/Döner_kebab_meat.jpg',
    'wrap': 'https://upload.wikimedia.org/wikipedia/commons/1/15/Shawarma_dürüm_2021.jpg',
    'shawarma': 'https://upload.wikimedia.org/wikipedia/commons/1/15/Shawarma_dürüm_2021.jpg',
    'guvec': 'https://upload.wikimedia.org/wikipedia/commons/7/75/Etli_güveç.jpg',
    'karisik-izgara': 'https://upload.wikimedia.org/wikipedia/commons/7/76/Mixed_grill.jpg',
    'kanat-izgara': 'https://upload.wikimedia.org/wikipedia/commons/2/25/Grilled_chicken_wings.jpg',
    'chicken-wings': 'https://upload.wikimedia.org/wikipedia/commons/2/25/Grilled_chicken_wings.jpg',
    'chicken-wings-2': 'https://upload.wikimedia.org/wikipedia/commons/2/25/Grilled_chicken_wings.jpg',
    'chicken-wings-3': 'https://upload.wikimedia.org/wikipedia/commons/2/25/Grilled_chicken_wings.jpg',
    'fried-chicken': 'https://upload.wikimedia.org/wikipedia/commons/5/52/Fried_chicken_plate.jpg',
    'nuggets': 'https://upload.wikimedia.org/wikipedia/commons/0/07/Chicken_nuggets_2.jpg',
    'nuggets-2': 'https://upload.wikimedia.org/wikipedia/commons/0/07/Chicken_nuggets_2.jpg',
    'somon': 'https://upload.wikimedia.org/wikipedia/commons/5/5e/Grilled_salmon_and_vegetables.jpg',
    'tavuk-izgara': 'https://upload.wikimedia.org/wikipedia/commons/b/b2/Grilled_chicken_breast.jpg',
    'balik-tava': 'https://upload.wikimedia.org/wikipedia/commons/4/42/Fried_fish_plate.jpg',
    'ahtapot': 'https://upload.wikimedia.org/wikipedia/commons/1/10/Grilled_octopus_1.jpg',
    'kumpir': 'https://upload.wikimedia.org/wikipedia/commons/6/64/Kumpir_%28Baked_potato%29.jpg',
    'kuru-fasulye': 'https://upload.wikimedia.org/wikipedia/commons/7/74/Kurufasulye.jpg',
    'kunefe': 'https://upload.wikimedia.org/wikipedia/commons/4/42/Knafeh_in_Nazareth%2C_Israel.png',
    'sutlac': 'https://upload.wikimedia.org/wikipedia/commons/8/87/Sütlaç.jpg',
    'kazandibi': 'https://upload.wikimedia.org/wikipedia/commons/2/2f/Kazandibi.jpg',
    'trilece': 'https://upload.wikimedia.org/wikipedia/commons/3/36/Tres_leches_cake.jpg',
    'kavun': 'https://upload.wikimedia.org/wikipedia/commons/e/e0/Cantaloupe_sliced.jpg',
    'dondurma': 'https://upload.wikimedia.org/wikipedia/commons/6/68/Ice_cream_bowl.jpg',
    'dondurma-2': 'https://upload.wikimedia.org/wikipedia/commons/8/87/Maraş_dondurma.jpg',
    'baklava': 'https://upload.wikimedia.org/wikipedia/commons/c/c7/Baklava_-_Turkish_special.jpg',
    'profiterol': 'https://upload.wikimedia.org/wikipedia/commons/4/4d/Profiteroles.jpg',
    'panna-cotta': 'https://upload.wikimedia.org/wikipedia/commons/8/8e/Panna_cotta_with_berries.jpg',
    'cikolatali-fondan': 'https://upload.wikimedia.org/wikipedia/commons/6/64/Chocolate_fondant.jpg',
    'brownie': 'https://upload.wikimedia.org/wikipedia/commons/8/87/Chocolate_brownie.jpg',
    'cheesecake': 'https://upload.wikimedia.org/wikipedia/commons/6/6b/Cheesecake_with_strawberries.jpg'
};

const basePath = 'D:/projcts/RestPROJ/apps/api/src/RestaurantOS.Api/media/stock';
const manifestPath = path.join(basePath, 'manifest.json');
let manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
let updated = 0;

function sleep(ms) { return new Promise(resolve => setTimeout(resolve, ms)); }

function downloadImage(url, dest) {
    return new Promise((resolve, reject) => {
        https.get(url, { headers: { 'User-Agent': 'RestaurantPhotoFixer/2.0' } }, (response) => {
            if (response.statusCode >= 300 && response.statusCode < 400 && response.headers.location) {
                return downloadImage(response.headers.location, dest).then(resolve).catch(reject);
            }
            if (response.statusCode !== 200) return reject(new Error('Status: ' + response.statusCode));
            
            const file = fs.createWriteStream(dest);
            response.pipe(file);
            file.on('finish', () => { file.close(resolve); });
        }).on('error', reject);
    });
}

(async function run() {
    for (let [id, url] of Object.entries(fixes)) {
        let photo = manifest.photos.find(p => p.id === id);
        if (!photo) continue;
        
        let dest = path.join(basePath, photo.file.replace('/', '\\'));
        try {
            await downloadImage(url, dest);
            photo.sourceUrl = url;
            console.log('OK ' + id);
            updated++;
            await sleep(1000); // Wait 1 second to avoid 429 Too Many Requests
        } catch (e) {
            console.log('FAIL ' + id + ' - ' + e.message);
            await sleep(2000); // Wait longer on failure
        }
    }
    
    fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 4), 'utf8');
    console.log('Batch 1 done. Updated ' + updated + ' photos.');
})();
