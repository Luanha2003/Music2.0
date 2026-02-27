/* ============================================================
   library.js - Quản lý thư viện cá nhân bằng localStorage
   ============================================================ */

const MusicLibrary = {
    KEYS: {
        SONGS: 'music2_fav_songs',
        ALBUMS: 'music2_saved_albums',
    },

    _getList(key) {
        try {
            return JSON.parse(localStorage.getItem(key)) || [];
        } catch {
            return [];
        }
    },

    _saveList(key, list) {
        localStorage.setItem(key, JSON.stringify(list));
    },

    // ── Songs ──
    isSongLiked(encodeId) {
        return this._getList(this.KEYS.SONGS).some(s => s.encodeId === encodeId);
    },

    toggleSong(song) {
        const list = this._getList(this.KEYS.SONGS);
        const idx = list.findIndex(s => s.encodeId === song.encodeId);
        if (idx >= 0) {
            list.splice(idx, 1);
            this._saveList(this.KEYS.SONGS, list);
            return false; // removed
        } else {
            list.unshift({
                encodeId: song.encodeId,
                title: song.title,
                artistsNames: song.artistsNames,
                thumbnailM: song.thumbnailM || song.thumbnail,
                duration: song.duration,
                album: song.album ? { encodeId: song.album.encodeId, title: song.album.title } : null,
            });
            this._saveList(this.KEYS.SONGS, list);
            return true; // added
        }
    },

    getLikedSongs() {
        return this._getList(this.KEYS.SONGS);
    },

    // ── Albums ──
    isAlbumSaved(encodeId) {
        return this._getList(this.KEYS.ALBUMS).some(a => a.encodeId === encodeId);
    },

    toggleAlbum(album) {
        const list = this._getList(this.KEYS.ALBUMS);
        const idx = list.findIndex(a => a.encodeId === album.encodeId);
        if (idx >= 0) {
            list.splice(idx, 1);
            this._saveList(this.KEYS.ALBUMS, list);
            return false;
        } else {
            list.unshift({
                encodeId: album.encodeId,
                title: album.title,
                thumbnailM: album.thumbnailM || album.thumbnail,
                artistsNames: album.artistsNames,
            });
            this._saveList(this.KEYS.ALBUMS, list);
            return true;
        }
    },

    getSavedAlbums() {
        return this._getList(this.KEYS.ALBUMS);
    },

    // ── Stats ──
    totalLiked() {
        return this.getLikedSongs().length;
    },
    totalAlbums() {
        return this.getSavedAlbums().length;
    }
};
