import moment from 'moment';



export class Utils {
    // LOCAL STORAGE
    public static getLocalStorage(key: string) {
        try {
            return JSON.parse(localStorage.getItem(key) || '');
        } catch (error) {
            return {};
        }
    }

    public static setLocalStorage(key: string, data: any) {
        localStorage.setItem(key, JSON.stringify(data));
    }

    public static clearLocalStorage() {
        localStorage.clear();
    }

    public static setSessionStorage(key: string, data: any) {
        sessionStorage.setItem(key, data);
    }

    public static getSessionStorage(key: string) {
        return sessionStorage.getItem(key);
    }

    public static clearSessionStorage() {
        sessionStorage.clear()
    }

    public static removeSessionStorage(key: string) {
        sessionStorage.removeItem(key)
    }

  

    public static refreshData(data: any) {
        return JSON.parse(JSON.stringify(data));
    }

    public static convertLowerCase(string: string = '') {
        if (string.length > 0) {
            return string.charAt(0).toLocaleLowerCase() + string.slice(1);
        }
        return '';
    }

    /**
     * đảo từ 1-12-2021 -> 2021-12-1
     * @param date
     * @returns
     */
    public static reverseDateString(date: string) {
        return date.split(/[-,/]/).reverse().join('-');
    }

    public static replaceAll(str: string, find: string, replace: string) {
        var escapedFind = find.replace(/([.*+?^=!:${}()|\[\]\/\\])/g, '\\$1');
        return str.replace(new RegExp(escapedFind, 'g'), replace);
    }

    public static transformMoney(num: number, ...args: any[]): string {
        const value = `${num}`;
        if (value === '' || value === null || typeof value === 'undefined') {
            return '';
        }

        let locales = 'vi-VN';
        const cur = Number(value);

        if (args.length > 0) {
            locales = args[0];
        }

        const result = new Intl.NumberFormat(locales).format(cur);
        return result === 'NaN' ? '' : result;
    }

    public static transformPercent(num: number, ...args: any[]): string {
        const value = `${num}`;
        if (value === '' || value === null || typeof value === 'undefined') {
            return '';
        }

        let locales = 'vi-VN';
        const cur = Number(value);

        if (args.length > 0) {
            locales = args[0];
        }

        const result = new Intl.NumberFormat(locales).format(cur);
        return result === 'NaN' ? '' : this.replaceAll(result, '.', ',');
    }

    // BLOCK REQUEST API AFTER 3s

    public static log(titleError: string, error?: any) {
        console.log(`%c ${titleError} `, 'background:black; color: red', error);
    }

    public static convertParamUrl(name: string, value: number | string | boolean) {
        return name + '=' + encodeURIComponent('' + value) + '&';
    }

    public static makeRandom(lengthOfCode: number = 100, possible?: string) {
        possible = 'AbBCcDdEeFfGgHhIiJjKkLlMmNnOoPpQqRrSsTtUuVvWwXxYyZz1234567890-_';
        let text = '';
        for (let i = 0; i < lengthOfCode; i++) {
            text += possible.charAt(Math.floor(Math.random() * possible.length));
        }
        //
        return text;
    }

    static convertVietnameseToEng(str: string, isKeepCase = false) {
        // if(!isKeepCase) {
        //     str= str.toLowerCase();
        // }
        str = str.replace(/à|á|ạ|ả|ã|â|ầ|ấ|ậ|ẩ|ẫ|ă|ằ|ắ|ặ|ẳ|ẵ/g, 'a');
        str = str.replace(/è|é|ẹ|ẻ|ẽ|ê|ề|ế|ệ|ể|ễ/g, 'e');
        str = str.replace(/ì|í|ị|ỉ|ĩ/g, 'i');
        str = str.replace(/ò|ó|ọ|ỏ|õ|ô|ồ|ố|ộ|ổ|ỗ|ơ|ờ|ớ|ợ|ở|ỡ/g, 'o');
        str = str.replace(/ù|ú|ụ|ủ|ũ|ư|ừ|ứ|ự|ử|ữ/g, 'u');
        str = str.replace(/ỳ|ý|ỵ|ỷ|ỹ/g, 'y');
        str = str.replace(/đ/g, 'd');

        str = str.replace(/À|Á|Ạ|Ả|Ã|Â|Ầ|Ấ|Ậ|Ẩ|Ẫ|Ă|Ằ|Ắ|Ặ|Ẳ|Ẵ/g, 'A');
        str = str.replace(/È|É|Ẹ|Ẻ|Ẽ|Ê|Ề|Ế|Ệ|Ể|Ễ/g, 'E');
        str = str.replace(/Ì|Í|Ị|Ỉ|Ĩ/g, 'I');
        str = str.replace(/Ò|Ó|Ọ|Ỏ|Õ|Ô|Ồ|Ố|Ộ|Ổ|Ỗ|Ơ|Ờ|Ớ|Ợ|Ở|Ỡ/g, 'O');
        str = str.replace(/Ù|Ú|Ụ|Ủ|Ũ|Ư|Ừ|Ứ|Ự|Ử|Ữ/g, 'U');
        str = str.replace(/Ỳ|Ý|Ỵ|Ỷ|Ỹ/g, 'Y');
        str = str.replace(/Đ/g, 'D');

        return str;
    }

    static formatDateCallApi(date: string | Date | null | undefined, format = 'YYYY-MM-DDTHH:mm:ss') {
        if (moment(date).isValid()) {
            return moment(date).format(format);
        }
        return ''
    }

    static formatDateView(date: string | Date | null | undefined, format = 'DD/MM/YYYY') {
        if (moment(date).isValid()) {
            return moment(date).format(format);
        }
        return ''
    }

    static base64UrlEncode(arrayBuffer: ArrayBuffer) {
        let str = String.fromCharCode(...new Uint8Array(arrayBuffer));
        return btoa(str).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    }

    /**
     * Chuẩn hoá đường dẫn thư mục người dùng nhập (vd "C:\Users\Admin\workspace\H49.61.8.2017.01")
     * về dạng phù hợp để gửi cho BE: bỏ khoảng trắng/dấu nháy bao quanh, đổi backslash -> forward slash,
     * gộp slash lặp (giữ nguyên định dạng ổ đĩa Windows mà Directory.Exists chấp nhận).
     */
    static normalizeFolderPath(path: string): string {
        if (!path) return '';
        let p = path.trim();
        if ((p.startsWith('"') && p.endsWith('"')) || (p.startsWith("'") && p.endsWith("'"))) {
            p = p.slice(1, -1).trim();
        }
        p = p.replace(/\\/g, '/').replace(/\/{2,}/g, '/');
        return p.replace(/\/+$/, '');
    }

   
}
