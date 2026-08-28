# Полная очистка Gorgon lab без затрагивания остальной системы

Если ты сделал только то, что было в прошлой инструкции, на основном Debian мы меняли только следующее:

- поставили `debootstrap` и `systemd-container`;
- создали `/var/lib/machines/gorgon-lab`;
- создали `/root/gorgon-audit`;
- могли скачать `/root/gorgon-sample.deb`;
- могли создать `/root/gorgon-control`;
- сам Gorgon должен был запускаться только внутри `gorgon-lab`, а не на хосте.

Ниже блок удаляет именно это. Он **не запускает `apt autoremove`** и не трогает nginx, Docker, панели, сайты, SSH, базы данных и остальные сервисы.

## Удалить всё за один раз

Запусти от `root` весь блок целиком:

```bash
set -Eeuo pipefail

printf '\n[1/6] Останавливаю одноразовый контейнер, если он ещё существует...\n'
if command -v machinectl >/dev/null 2>&1; then
    machinectl terminate gorgon-lab 2>/dev/null || true
fi

printf '\n[2/6] На всякий случай останавливаю Gorgon на хосте, если он туда случайно попал...\n'
systemctl disable --now gorgon-stress 2>/dev/null || true
pkill -f '/opt/gorgon-stress/gorgon' 2>/dev/null || true

printf '\n[3/6] Если бинарник Gorgon принадлежит Debian-пакету — удаляю только этот пакет...\n'
GORGON_PKG="$(dpkg-query -S /opt/gorgon-stress/gorgon 2>/dev/null | head -n1 | cut -d: -f1 || true)"
if [ -n "$GORGON_PKG" ] && dpkg-query -W "$GORGON_PKG" >/dev/null 2>&1; then
    apt-get purge -y "$GORGON_PKG"
fi

printf '\n[4/6] Удаляю только созданные нами файлы и контейнер...\n'
rm -rf -- /var/lib/machines/gorgon-lab
rm -f  -- /root/gorgon-sample.deb
rm -rf -- /root/gorgon-control
rm -rf -- /root/gorgon-audit
rm -rf -- /opt/gorgon-stress

# Удаляем возможные оставшиеся unit-файлы только с точным именем Gorgon.
rm -f -- \
  /etc/systemd/system/gorgon-stress.service \
  /usr/lib/systemd/system/gorgon-stress.service \
  /lib/systemd/system/gorgon-stress.service
systemctl daemon-reload 2>/dev/null || true
systemctl reset-failed gorgon-stress 2>/dev/null || true

printf '\n[5/6] Удаляю два инструмента, которые мы ставили специально для лаборатории...\n'
# ВАЖНО: autoremove здесь намеренно НЕ используется, чтобы не снести
# какие-либо старые пакеты системы, которые apt уже считает ненужными.
apt-get purge -y debootstrap systemd-container || true

printf '\n[6/6] Проверка...\n'
LEFT=0

for p in \
  /var/lib/machines/gorgon-lab \
  /root/gorgon-sample.deb \
  /root/gorgon-control \
  /root/gorgon-audit \
  /opt/gorgon-stress; do
    if [ -e "$p" ]; then
        echo "ОСТАЛОСЬ: $p"
        LEFT=1
    fi
done

if systemctl list-unit-files --no-pager 2>/dev/null | grep -qi '^gorgon-stress'; then
    echo 'ОСТАЛСЯ systemd unit gorgon-stress'
    LEFT=1
fi

if pgrep -f '/opt/gorgon-stress/gorgon' >/dev/null 2>&1; then
    echo 'ОСТАЛСЯ процесс Gorgon'
    LEFT=1
fi

if ss -lntup 2>/dev/null | grep -q ':777'; then
    echo 'ВНИМАНИЕ: порт 777 всё ещё кем-то занят (это не обязательно Gorgon):'
    ss -lntup 2>/dev/null | grep ':777' || true
    LEFT=1
fi

if [ "$LEFT" -eq 0 ]; then
    echo
    echo 'ГОТОВО: следов нашей Gorgon-lab установки не найдено.'
    echo 'Остальные сервисы и пакеты системы намеренно не трогались.'
else
    echo
    echo 'Очистка завершена, но выше есть пункты, которые надо проверить вручную.'
fi
```

## Почему здесь нет `apt autoremove`

`apt autoremove` может удалить не только зависимости, появившиеся из-за этой лаборатории, но и старые пакеты сервера, которые уже раньше были помечены как автоматически установленные и теперь считаются ненужными. Поэтому для безопасной очистки основной системы мы его специально не запускаем.

После `apt-get purge debootstrap systemd-container` могут остаться несколько маленьких библиотек или зависимостей, если apt поставил их вместе с этими пакетами. Они безвредны. Лучше оставить пару лишних системных библиотек, чем ради нескольких мегабайт случайно удалить что-то, что использует другой сервис.

## Короткая проверка после перезагрузки

```bash
systemctl list-unit-files --no-pager | grep -i gorgon || true
ps auxww | grep -i '[g]orgon' || true
find /opt /etc/systemd /usr/lib/systemd /lib/systemd -iname '*gorgon*' -print 2>/dev/null
ss -lntup | grep ':777' || true
```

Если вывод пустой, кроме случая, когда порт `777` занимает какой-то другой твой сервис, то всё, что мы делали для этой идеи, удалено.
