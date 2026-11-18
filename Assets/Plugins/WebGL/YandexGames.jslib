// ВАЖНО: emscripten 3.1.39 (Unity 6) не подтягивает зависимости автоматически.
// Без явных __deps объявление $YandexGlue вырезается из сборки, а функции _YG_*
// остаются и падают в рантайме с "YandexGlue is not defined" -> игра зависает.
// Поэтому у КАЖДОЙ функции ниже указано YGDEPS.
mergeInto(LibraryManager.library, {
  $YandexGlue: {
    ysdk: null,
    payments: null,
    player: null,
    counterId: 0,
    leaderboardsProjects: {},
    // Лидерборды. Яндекс помечает ysdk.getLeaderboards() как устаревший и
    // требует ysdk.leaderboards — в консоли это всплывало как ошибка
    // "ysdk.getLeaderboards() is deprecated". Новый способ отдаёт промис
    // один раз на игру, поэтому кэшируем: иначе каждый запрос к таблице
    // дёргает SDK заново. Старый оставлен запасным — на случай, если на
    // площадке окажется SDK без нового поля.
    leaderboards: function () {
      if (!YandexGlue.ysdk) return Promise.reject("no sdk");
      var ysdk = YandexGlue.ysdk;
      if (ysdk.leaderboards) {
        var key = "default";
        if (!YandexGlue.leaderboardsProjects[key]) {
          YandexGlue.leaderboardsProjects[key] = Promise.resolve(ysdk.leaderboards);
        }
        return YandexGlue.leaderboardsProjects[key];
      }
      return ysdk.getLeaderboards();
    },
    send: function (method, payload) {
      payload = (payload === undefined || payload === null) ? "" : String(payload);
      try {
        SendMessage("YandexGames", method, payload);
        return;
      } catch (e) {
      }
      try {
        if (typeof window !== "undefined") {
          var inst = window.unityInstance || window.gameInstance;
          if (inst && typeof inst.SendMessage === "function") {
            inst.SendMessage("YandexGames", method, payload);
            return;
          }
        }
      } catch (e) {
      }
      console.warn("[YandexGames] cannot reach Unity for " + method + ": " + payload);
    },
    fail: function (method, err) {
      var msg = "error";
      try {
        if (err === undefined || err === null) msg = "unknown";
        else if (typeof err === "string") msg = err;
        else if (err.message) msg = err.message;
        else msg = String(err);
      } catch (e) {
        msg = "error";
      }
      YandexGlue.send(method, msg);
    }
  },

  YG_Init: function () {
    if (typeof YaGames === "undefined") {
      YandexGlue.send("OnSdkError", "YaGames undefined (not on Yandex Games)");
      return;
    }
    YaGames.init().then(function (ysdk) {
      YandexGlue.ysdk = ysdk;
      var lang = "ru";
      try {
        if (ysdk && ysdk.environment && ysdk.environment.i18n && ysdk.environment.i18n.lang) {
          lang = ysdk.environment.i18n.lang;
        }
      } catch (e) {
      }
      YandexGlue.send("OnSdkInit", lang);
    }).catch(function (e) {
      YandexGlue.fail("OnSdkError", e);
    });
  },
  YG_Init__deps: ["$YandexGlue"],

  YG_Ready: function () {
    if (YandexGlue.ysdk && YandexGlue.ysdk.features && YandexGlue.ysdk.features.LoadingAPI) {
      try {
        YandexGlue.ysdk.features.LoadingAPI.ready();
      } catch (e) {
      }
    }
  },
  YG_Ready__deps: ["$YandexGlue"],

  YG_ShowFullscreen: function () {
    if (!YandexGlue.ysdk || !YandexGlue.ysdk.adv) {
      YandexGlue.send("OnFullscreenClose", "0");
      return;
    }
    YandexGlue.ysdk.adv.showFullscreenAdv({
      callbacks: {
        onOpen: function () { YandexGlue.send("OnFullscreenOpen", "1"); },
        onClose: function (wasShown) { YandexGlue.send("OnFullscreenClose", wasShown ? "1" : "0"); },
        onError: function (e) { YandexGlue.fail("OnFullscreenError", e); }
      }
    });
  },
  YG_ShowFullscreen__deps: ["$YandexGlue"],

  YG_ShowRewarded: function () {
    if (!YandexGlue.ysdk || !YandexGlue.ysdk.adv) {
      YandexGlue.send("OnRewardedClose", "0");
      return;
    }
    YandexGlue.ysdk.adv.showRewardedVideo({
      callbacks: {
        onOpen: function () { YandexGlue.send("OnRewardedOpen", "1"); },
        onRewarded: function () { YandexGlue.send("OnRewarded", "1"); },
        onClose: function () { YandexGlue.send("OnRewardedClose", "1"); },
        onError: function (e) { YandexGlue.fail("OnRewardedError", e); }
      }
    });
  },
  YG_ShowRewarded__deps: ["$YandexGlue"],

  YG_LoadData: function () {
    if (!YandexGlue.ysdk) {
      YandexGlue.send("OnDataError", "no sdk");
      return;
    }
    YandexGlue.ysdk.getPlayer({ scopes: false }).then(function (player) {
      YandexGlue.player = player;
      return player.getData(["unity_save"]);
    }).then(function (data) {
      var save = "";
      if (data && data.unity_save) {
        save = String(data.unity_save);
      }
      YandexGlue.send("OnDataLoaded", save);
    }).catch(function (e) {
      YandexGlue.fail("OnDataError", e);
    });
  },
  YG_LoadData__deps: ["$YandexGlue"],

  YG_SaveData: function (jsonPtr) {
    var save = UTF8ToString(jsonPtr);
    if (!YandexGlue.ysdk) return;
    var doSave = function (player) {
      var obj = {};
      try {
        obj.unity_save = save;
      } catch (e) {
        obj.unity_save = "";
      }
      return player.setData(obj, true);
    };
    var chain = YandexGlue.player ? doSave(YandexGlue.player) : YandexGlue.ysdk.getPlayer({ scopes: false }).then(doSave);
    chain.then(function () { YandexGlue.send("OnDataSaved", "1"); }).catch(function (e) { YandexGlue.fail("OnDataError", e); });
  },
  YG_SaveData__deps: ["$YandexGlue"],

  YG_SetLeaderboardScore: function (namePtr, score) {
    var name = UTF8ToString(namePtr);
    if (!YandexGlue.ysdk) return;
    YandexGlue.leaderboards().then(function (lb) {
      return lb.setLeaderboardScore(name, score);
    }).then(function () { YandexGlue.send("OnScoreSet", String(score)); }).catch(function (e) { YandexGlue.fail("OnLeaderboardError", e); });
  },
  YG_SetLeaderboardScore__deps: ["$YandexGlue"],

  YG_GetLeaderboardEntries: function (namePtr, top, around) {
    var name = UTF8ToString(namePtr);
    if (!YandexGlue.ysdk) { YandexGlue.send("OnLeaderboardError", "no sdk"); return; }
    YandexGlue.leaderboards().then(function (lb) {
      return lb.getLeaderboardEntries(name, { quantityTop: top, includeUser: true, quantityAround: around });
    }).then(function (res) {
      YandexGlue.send("OnLeaderboardEntries", JSON.stringify(res || {}));
    }).catch(function (e) { YandexGlue.fail("OnLeaderboardError", e); });
  },
  YG_GetLeaderboardEntries__deps: ["$YandexGlue"],

  YG_LoadPayments: function () {
    if (!YandexGlue.ysdk) { YandexGlue.send("OnPurchasesError", "no sdk"); return; }
    YandexGlue.ysdk.getPayments({ signed: false }).then(function (pay) {
      YandexGlue.payments = pay;
      return pay.getPurchases();
    }).then(function (list) {
      YandexGlue.send("OnPurchasesLoaded", JSON.stringify(list || []));
    }).catch(function (e) { YandexGlue.fail("OnPurchasesError", e); });
  },
  YG_LoadPayments__deps: ["$YandexGlue"],

  YG_Purchase: function (idPtr) {
    var id = UTF8ToString(idPtr);
    if (!YandexGlue.payments) { YandexGlue.send("OnPurchaseError", "payments not ready"); return; }
    YandexGlue.payments.purchase({ id: id }).then(function (pur) {
      YandexGlue.send("OnPurchaseResult", JSON.stringify(pur || {}));
    }).catch(function (e) { YandexGlue.fail("OnPurchaseError", e); });
  },
  YG_Purchase__deps: ["$YandexGlue"],

  YG_Consume: function (tokenPtr) {
    var token = UTF8ToString(tokenPtr);
    if (!YandexGlue.payments) return;
    YandexGlue.payments.consumePurchase(token).then(function () { YandexGlue.send("OnPurchaseConsumed", "1"); }).catch(function (e) { YandexGlue.fail("OnPurchaseError", e); });
  },
  YG_Consume__deps: ["$YandexGlue"],

  YG_MetricaGoal: function (namePtr) {
    var name = UTF8ToString(namePtr);
    if (YandexGlue.counterId && typeof ym === "function") {
      try { ym(YandexGlue.counterId, "reachGoal", name); } catch (e) {}
    }
  },
  YG_MetricaGoal__deps: ["$YandexGlue"],

  YG_IsAvailable: function () {
    return YandexGlue.ysdk ? 1 : 0;
  },
  YG_IsAvailable__deps: ["$YandexGlue"]
});
