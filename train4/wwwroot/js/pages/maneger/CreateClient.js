document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('createClientForm');
    const password = document.getElementById('password');
    const confirmPassword = document.getElementById('confirmPassword');

    // Элементы платежной информации
    const paymentType = document.getElementById('paymentType');
    const cardContainer = document.getElementById('cardContainer');
    const ibanContainer = document.getElementById('ibanContainer');
    const cardNumber = document.getElementById('cardNumber');
    const cardMonth = document.getElementById('cardMonth');
    const cardYear = document.getElementById('cardYear');
    const ibanField = document.getElementById('ibanField');
    const hiddenPaymentInfo = document.getElementById('hiddenPaymentInfo');

    // Обработка выбора типа оплаты
    paymentType.addEventListener('change', function () {
        // Скрываем все контейнеры
        cardContainer.style.display = 'none';
        ibanContainer.style.display = 'none';

        // Сбрасываем required атрибуты
        cardNumber.required = false;
        cardMonth.required = false;
        cardYear.required = false;
        ibanField.required = false;

        if (this.value === 'card') {
            cardContainer.style.display = 'block';
        } else if (this.value === 'iban') {
            ibanContainer.style.display = 'block';
        }
    });

    // Форматирование номера карты
    cardNumber.addEventListener('input', function () {
        let value = this.value.replace(/\D/g, '');
        value = value.substring(0, 16);
        let formattedValue = value.replace(/(.{4})/g, '$1 ').trim();
        this.value = formattedValue;
    });

    // Форматирование месяца
    cardMonth.addEventListener('input', function () {
        let value = this.value.replace(/\D/g, '');
        if (value.length >= 1) {
            let month = parseInt(value);
            if (month > 12) {
                value = '12';
            } else if (month < 1 && value.length === 2) {
                value = '01';
            }
        }
        this.value = value.substring(0, 2);
    });

    // Форматирование года
    cardYear.addEventListener('input', function () {
        let value = this.value.replace(/\D/g, '');
        this.value = value.substring(0, 2);
    });

    // Форматирование IBAN
    ibanField.addEventListener('input', function () {
        let value = this.value.replace(/[^A-Z0-9]/gi, '').toUpperCase();

        if (value.length >= 2 && !value.startsWith('UA')) {
            value = 'UA' + value.substring(0, 32);
        }

        let formattedValue = value.replace(/(.{4})/g, '$1 ').trim();
        this.value = formattedValue;
    });

    // Обработка отправки формы
    form.addEventListener('submit', function (e) {
        // Проверка паролей
        if (password.value !== confirmPassword.value) {
            e.preventDefault();
            alert('Паролі не співпадають. Будь ласка, перевірте.');
            return;
        }

        // Формирование платежной информации
        let paymentInfo = '';

        if (paymentType.value === 'card') {
            const cardNum = cardNumber.value.replace(/\s/g, '');
            const month = cardMonth.value.padStart(2, '0');
            const year = cardYear.value.padStart(2, '0');

            if (cardNum.length === 16 && month && year) {
                paymentInfo = `Card: ${cardNumber.value} Valid: ${month}/${year}`;
            }
        } else if (paymentType.value === 'iban') {
            const iban = ibanField.value.replace(/\s/g, '');
            if (iban.length >= 28 && iban.startsWith('UA')) {
                paymentInfo = `IBAN: ${iban}`;
            }
        } else if (paymentType.value === 'cash') {
            paymentInfo = 'Cash only';
        }

        // Записываем в скрытое поле
        hiddenPaymentInfo.value = paymentInfo;
    });
});

// Функция для показа/скрытия пароля
function togglePassword(fieldId) {
    const field = document.getElementById(fieldId);
    const eye = document.getElementById(fieldId + '-eye');

    if (field.type === 'password') {
        field.type = 'text';
        eye.classList.remove('bi-eye');
        eye.classList.add('bi-eye-slash');
    } else {
        field.type = 'password';
        eye.classList.remove('bi-eye-slash');
        eye.classList.add('bi-eye');
    }
}
