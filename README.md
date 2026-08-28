# Gorgon Stress: безопасно скачать образец, запустить на Debian и потом вычистить

Источник разбора: https://rt-solar.ru/solar-4rays/blog/4690/

Статья Solar 4RAYS разбирает Gorgon Stress Tester версии 1.9.7.9. Это DDoS-инструмент, поэтому нормальный способ посмотреть его вживую — **не ставить его прямо в основную Debian-систему**, а поднять одноразовое изолированное окружение без внешней сети, запустить сервис там и после теста удалить окружение целиком.

Это еще и единственный действительно надежный вариант, если задача звучит как «потом полностью вычистить». Если установить `.deb` прямо на хост, пакет меняет состояние `dpkg`, systemd, Tor, журналы и apt-метаданные. Удалить сам бинарник можно, но вернуть систему буквально байт-в-байт в прежнее состояние без снапшота уже нельзя.

---

## Что именно делает пакет по данным Solar 4RAYS

Статья подтверждает следующее:

- Gorgon распространялся как `.deb`;
- основная директория установки: `/opt/gorgon-stress/`;
- бинарник запускается как root;
- systemd-сервис называется `gorgon-stress`;
- `ExecStart=/opt/gorgon-stress/gorgon`;
- сервис включается в автозапуск;
- в unit-файле задан автоматический рестарт;
- postinst перезапускает и включает Tor;
- Gorgon поднимает HTTP-сервер на `0.0.0.0:777`;
- в исследованной конфигурации были дефолтные credentials `admin / admin1234`;
- пакет использует Tor и SOCKS-порты;
- среди зависимостей Solar указывают Tor, Xvfb и OpenJDK 11.

Поэтому ставить этот `.deb` непосредственно на боевой сервер — плохая идея даже просто «на минуту посмотреть».

---

# Вариант, который я бы использовал: одноразовый systemd-nspawn контейнер

Ниже Gorgon запускается на **том же Debian-сервере**, но его файловая система и сеть изолированы от основной системы.

Главное правило: когда сам образец уже установлен и запускается, контейнер стартует с `--private-network`, то есть наружу он ничего отправлять не должен.

## 1. Сохрани базовое состояние хоста

```bash
sudo -i
mkdir -p /root/gorgon-audit

dpkg-query -W -f='${binary:Package}\t${Version}\n' | sort > /root/gorgon-audit/packages.before
systemctl list-unit-files --no-pager > /root/gorgon-audit/units.before
ss -lntup > /root/gorgon-audit/sockets.before

if [ -f /etc/tor/torrc ]; then
    cp -a /etc/tor/torrc /root/gorgon-audit/torrc.before
    sha256sum /etc/tor/torrc > /root/gorgon-audit/torrc.before.sha256
fi
```

Если сервер у провайдера умеет snapshots — **сделай snapshot перед всем этим**. Это лучше любого cleanup-скрипта.

---

## 2. Установи только инструменты для одноразового контейнера

```bash
apt update
apt install -y debootstrap systemd-container
```

Проверить:

```bash
systemd-nspawn --version
machinectl --version
```

---

## 3. Создай чистую Debian-систему отдельно от хоста

Для Debian 12:

```bash
mkdir -p /var/lib/machines/gorgon-lab

debootstrap bookworm /var/lib/machines/gorgon-lab https://deb.debian.org/debian
```

После этого `/var/lib/machines/gorgon-lab` — отдельная файловая система будущего контейнера.

Gorgon в основной Debian пока вообще не установлен.

---

# Где взять `.deb`

В статье Solar 4RAYS **нет прямой ссылки на образец и нет опубликованного SHA-256 анализируемого файла**. Там сказано только, что версии распространялись через Telegram-каналы.

Поэтому я не стал бы качать первый попавшийся файл с названием `gorgon-stress.deb`: это запросто может быть уже перепакованная версия с дополнительным бэкдором.

Если у тебя уже есть URL конкретного образца, скачивай файл как данные, но **не устанавливай его на хост**:

```bash
export SAMPLE_URL='https://.../gorgon.deb'

curl -fL --proto '=https' --tlsv1.2 \
  -o /root/gorgon-sample.deb \
  "$SAMPLE_URL"

sha256sum /root/gorgon-sample.deb
file /root/gorgon-sample.deb
```

Сначала просто посмотри содержимое:

```bash
dpkg-deb --info /root/gorgon-sample.deb

dpkg-deb --contents /root/gorgon-sample.deb | less

rm -rf /root/gorgon-control
mkdir /root/gorgon-control

dpkg-deb --control /root/gorgon-sample.deb /root/gorgon-control

find /root/gorgon-control -maxdepth 1 -type f -print
```

Особенно интересны:

```bash
sed -n '1,240p' /root/gorgon-control/control 2>/dev/null
sed -n '1,300p' /root/gorgon-control/postinst 2>/dev/null
sed -n '1,300p' /root/gorgon-control/preinst 2>/dev/null
sed -n '1,300p' /root/gorgon-control/prerm 2>/dev/null
sed -n '1,300p' /root/gorgon-control/postrm 2>/dev/null
```

Так ты увидишь реальные install/remove scripts **до их выполнения**.

---

## 4. Скопируй `.deb` внутрь лаборатории

```bash
install -m 0600 \
  /root/gorgon-sample.deb \
  /var/lib/machines/gorgon-lab/root/gorgon-sample.deb
```

Проверь:

```bash
ls -lh /var/lib/machines/gorgon-lab/root/gorgon-sample.deb
```

---

## 5. Заранее поставь обычные зависимости

Пока Gorgon **еще не установлен**, можно подготовить контейнер:

```bash
chroot /var/lib/machines/gorgon-lab /bin/bash
```

Внутри:

```bash
apt update
apt install -y tor xvfb curl ca-certificates
```

Solar указывали OpenJDK 11 как зависимость анализируемой версии. Конкретное имя доступного Java-пакета зависит от релиза Debian и от полей `Depends:` самого `.deb`, поэтому сначала смотри:

```bash
dpkg-deb -f /root/gorgon-sample.deb Depends
```

Не угадывай зависимость вслепую.

После подготовки:

```bash
exit
```

---

# 6. Запусти лабораторию БЕЗ внешней сети

```bash
systemd-nspawn \
  -D /var/lib/machines/gorgon-lab \
  --machine=gorgon-lab \
  --boot \
  --private-network
```

Оставь этот процесс запущенным.

Во второй SSH-сессии на хосте:

```bash
machinectl status gorgon-lab
machinectl shell root@gorgon-lab
```

Теперь ты внутри контейнера.

Проверь интерфейсы:

```bash
ip addr
ip route
```

У контейнера не должно быть нормального маршрута в интернет.

---

# 7. Установи образец уже внутри изолированной машины

Внутри `gorgon-lab`:

```bash
dpkg -i /root/gorgon-sample.deb
```

Если `dpkg` ругается на отсутствующую зависимость — **не включай внешнюю сеть ради Gorgon**. Выйди, останови лабораторию, добавь нужную обычную зависимость в чистый rootfs и снова запускай контейнер с `--private-network`.

Посмотри, что появилось:

```bash
systemctl status gorgon-stress --no-pager -l
systemctl cat gorgon-stress

ls -la /opt/gorgon-stress

ss -lntup
```

По исследованию Solar сервис должен слушать порт `777`.

Проверка только изнутри лаборатории:

```bash
curl -v --max-time 3 http://127.0.0.1:777/ 2>&1 | head -n 80
```

Если сервис не стартовал автоматически:

```bash
systemctl start gorgon-stress
systemctl status gorgon-stress --no-pager -l
```

На этом уже можно подтвердить, что бинарник запускается, unit рабочий и веб-сервис поднимается.

**Не запускай режимы атаки и не пробрасывай 777 наружу.** Для проверки установки и поведения пакета это вообще не требуется.

---

# 8. Посмотри, что он реально изменил

Все еще внутри контейнера:

```bash
dpkg-query -S /opt/gorgon-stress/gorgon 2>/dev/null || true

systemctl is-enabled gorgon-stress 2>/dev/null || true
systemctl is-enabled tor 2>/dev/null || true

systemctl status tor --no-pager -l 2>/dev/null || true

find /etc/systemd /usr/lib/systemd /lib/systemd \
  -type f -iname '*gorgon*' -print 2>/dev/null

find /opt -maxdepth 3 -iname '*gorgon*' -print 2>/dev/null

ss -lntup | grep -E '(:777|:905[0-9]|:91[0-9][0-9])' || true
```

Это как раз полезнее, чем верить статье на слово: ты увидишь поведение именно того `.deb`, который скачал.

---

# 9. Остановка

Внутри контейнера:

```bash
systemctl disable --now gorgon-stress 2>/dev/null || true
systemctl stop tor 2>/dev/null || true
```

Выйди:

```bash
exit
```

На хосте:

```bash
machinectl terminate gorgon-lab
```

Проверь:

```bash
machinectl list
```

`gorgon-lab` больше не должен работать.

---

# 10. Полное удаление лаборатории

Вот здесь и есть главное преимущество контейнера: вместо попытки угадать каждый файл Gorgon мы удаляем **всю систему, в которой он когда-либо выполнялся**.

```bash
rm -rf /var/lib/machines/gorgon-lab
```

Удаляем сам `.deb` с хоста:

```bash
rm -f /root/gorgon-sample.deb
rm -rf /root/gorgon-control
```

Проверка:

```bash
test ! -e /var/lib/machines/gorgon-lab && echo 'lab filesystem removed'
test ! -e /root/gorgon-sample.deb && echo 'sample removed'
```

На основном сервере сам пакет Gorgon вообще никогда не устанавливался.

---

# 11. Проверка основного Debian после удаления

```bash
systemctl list-units --all --no-pager | grep -i gorgon || true
systemctl list-unit-files --no-pager | grep -i gorgon || true

ps auxww | grep -i '[g]orgon' || true

ss -lntup | grep ':777' || true

find /opt /etc/systemd /usr/lib/systemd /lib/systemd \
  -iname '*gorgon*' -print 2>/dev/null
```

В нормальном варианте здесь должно быть пусто.

---

# 12. Удалять ли `debootstrap` и `systemd-container`

Это обычные Debian-инструменты, не части Gorgon.

Если ты поставил их только для этого теста и они больше не нужны:

```bash
apt purge -y debootstrap systemd-container
apt autoremove --purge
```

Но `autoremove` **сначала просмотри перед подтверждением**, особенно на сервере с кучей сервисов.

---

# Если ты уже по ошибке установил Gorgon ПРЯМО НА ХОСТ

Тогда сначала останови его:

```bash
sudo systemctl disable --now gorgon-stress 2>/dev/null || true
sudo pkill -f '/opt/gorgon-stress/gorgon' 2>/dev/null || true
```

Узнай реальное имя Debian-пакета:

```bash
dpkg-query -S /opt/gorgon-stress/gorgon 2>/dev/null
```

Например результат может выглядеть как:

```text
PACKAGE_NAME: /opt/gorgon-stress/gorgon
```

Тогда:

```bash
PKG="$(dpkg-query -S /opt/gorgon-stress/gorgon 2>/dev/null | head -n1 | cut -d: -f1)"
printf 'package=%s\n' "$PKG"
```

Если значение выглядит корректно:

```bash
sudo apt purge "$PKG"
```

После package purge проверь остатки:

```bash
sudo systemctl daemon-reload
sudo systemctl reset-failed

systemctl list-unit-files --no-pager | grep -i gorgon || true
ps auxww | grep -i '[g]orgon' || true
ss -lntup | grep ':777' || true

sudo find /opt /etc/systemd /usr/lib/systemd /lib/systemd \
  -iname '*gorgon*' -print 2>/dev/null
```

Если `/opt/gorgon-stress` остался после purge и ты уже проверил, что он не принадлежит никакому нужному пакету:

```bash
sudo rm -rf /opt/gorgon-stress
```

Проверь unit-файлы:

```bash
sudo find /etc/systemd /usr/lib/systemd /lib/systemd \
  -type f -iname '*gorgon*' -print
```

Если остался именно `gorgon-stress.service`, удаляй конкретный найденный файл, затем:

```bash
sudo systemctl daemon-reload
sudo systemctl reset-failed
```

---

# Отдельно про Tor — тут легко снести лишнее

По статье postinst Gorgon делает примерно следующее: перезапускает Tor, включает его и стартует вместе с Gorgon.

Поэтому **не надо тупо делать `apt purge tor`**, если Tor был установлен на сервере еще до эксперимента.

Сначала:

```bash
systemctl status tor --no-pager -l 2>/dev/null || true
systemctl is-enabled tor 2>/dev/null || true

dpkg-query -W tor 2>/dev/null || true

ls -la /etc/tor 2>/dev/null || true
```

Если перед тестом был сохранен `/root/gorgon-audit/torrc.before`, сравни:

```bash
diff -u /root/gorgon-audit/torrc.before /etc/tor/torrc || true
```

Если Gorgon действительно заменил конфиг, восстанови сохраненный:

```bash
sudo cp -a /root/gorgon-audit/torrc.before /etc/tor/torrc
sudo systemctl restart tor
```

Если Tor до Gorgon **вообще не был установлен** и появился исключительно как зависимость этого пакета, тогда уже можно рассматривать:

```bash
sudo apt purge tor
```

Но сначала проверь, что его не использует другой сервис.

---

# Проверка установленных пакетов после эксперимента

Если делал baseline в начале:

```bash
dpkg-query -W -f='${binary:Package}\t${Version}\n' | sort > /root/gorgon-audit/packages.after

comm -3 \
  /root/gorgon-audit/packages.before \
  /root/gorgon-audit/packages.after
```

Это покажет, какие пакеты реально отличаются.

Для systemd:

```bash
systemctl list-unit-files --no-pager > /root/gorgon-audit/units.after

diff -u \
  /root/gorgon-audit/units.before \
  /root/gorgon-audit/units.after || true
```

Для listening sockets:

```bash
ss -lntup > /root/gorgon-audit/sockets.after

diff -u \
  /root/gorgon-audit/sockets.before \
  /root/gorgon-audit/sockets.after || true
```

---

# Что значит «ПОЛНОСТЬЮ вычистить» на практике

Есть три разных уровня.

### 1. Удалить программу

`apt purge`, удалить `/opt/gorgon-stress`, unit и остановить процессы.

Это легко.

### 2. Удалить все runtime-хвосты

Нужно дополнительно проверять Tor, systemd enable state, зависимости, конфиги, процессы, открытые порты и файлы.

Это уже заметно сложнее.

### 3. Вернуть сервер буквально в состояние до запуска неизвестного root-бинарника

После прямой установки на host гарантировать это нельзя.

Root-процесс теоретически может изменить **любой** доступный файл. Кроме того, останутся записи в journal, apt/dpkg logs и метаданных файловой системы.

Поэтому единственные нормальные способы получить настоящий clean rollback:

- snapshot VM до запуска → rollback;
- отдельная одноразовая VM → удалить VM;
- отдельный `systemd-nspawn` rootfs без bind mounts → удалить весь rootfs.

Для твоей задачи я бы выбрал третий вариант: он выполняется прямо на Debian-сервере, Gorgon реально стартует, но после `machinectl terminate` + удаления `/var/lib/machines/gorgon-lab` от его окружения ничего не остается на хосте, кроме заранее скачанного `.deb` и обычных инструментов контейнеризации, которые тоже можно удалить.