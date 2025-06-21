document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('registerForm');
    const password = document.getElementById('password');
    const confirmPassword = document.getElementById('confirmPassword');

    // Елементи платіжної інформації
    const paymentType = document.getElementById('paymentType');
    const cardContainer = document.getElementById('cardContainer');
    const ibanContainer = document.getElementById('ibanContainer');
    const cardNumber = document.getElementById('cardNumber');
    const cardMonth = document.getElementById('cardMonth');
    const cardYear = document.getElementById('cardYear');
    const cardValidityDisplay = document.getElementById('cardValidityDisplay');
    const ibanField = document.getElementById('ibanField');
    const hiddenPaymentInfo = document.getElementById('hiddenPaymentInfo');

    // Обробка вибору типу оплати
    paymentType.addEventListener('change', function () {
        // Приховуємо всі контейнери
        cardContainer.style.display = 'none';
        ibanContainer.style.display = 'none';

        // Скидаємо required атрибути
        cardNumber.required = false;
        cardMonth.required = false;
        cardYear.required = false;
        ibanField.required = false;

        if (this.value === 'card') {
            cardContainer.style.display = 'block';
            cardNumber.required = true;
            cardMonth.required = true;
            cardYear.required = true;
        } else if (this.value === 'iban') {
            ibanContainer.style.display = 'block';
            ibanField.required = true;
        }
    });

    // Форматування номера карти
    cardNumber.addEventListener('input', function () {
        let value = this.value.replace(/\D/g, ''); // Видаляємо все крім цифр
        value = value.substring(0, 16); // Максимум 16 цифр

        // Додаємо пробіли кожні 4 цифри
        let formattedValue = value.replace(/(.{4})/g, '$1 ').trim();
        this.value = formattedValue;

        // Валідація номера карти
        validateCardNumber(value);
    });

    // Форматування місяця
    cardMonth.addEventListener('input', function () {
        let value = this.value.replace(/\D/g, ''); // Тільки цифри

        if (value.length >= 1) {
            let month = parseInt(value);
            if (month > 12) {
                value = '12';
            } else if (month < 1 && value.length === 2) {
                value = '01';
            }
        }

        this.value = value.substring(0, 2);
        updateValidityDisplay();
        validateCardExpiry();
    });

    // Форматування року
    cardYear.addEventListener('input', function () {
        let value = this.value.replace(/\D/g, ''); // Тільки цифри
        this.value = value.substring(0, 2);
        updateValidityDisplay();
        validateCardExpiry();
    });

    // Оновлення відображення терміну дії
    function updateValidityDisplay() {
        const month = cardMonth.value.padStart(2, '0');
        const year = cardYear.value.padStart(2, '0');
        cardValidityDisplay.textContent = `${month}/${year}`;
    }

    // Валідація номера карти (приймає будь-які 16 цифр)
    function validateCardNumber(cardNum) {
        const cardNumberField = document.getElementById('cardNumber');

        if (cardNum.length === 16) {
            // Перевіряємо що всі символи - це цифри
            if (/^\d{16}$/.test(cardNum)) {
                cardNumberField.classList.remove('is-invalid');
                cardNumberField.classList.add('is-valid');
            } else {
                cardNumberField.classList.remove('is-valid');
                cardNumberField.classList.add('is-invalid');
            }
        } else {
            cardNumberField.classList.remove('is-valid', 'is-invalid');
        }
    }

    // Алгоритм Луна для валідації карти (виправлений)
    function isValidCardNumber(cardNumber) {
        // Перевіряємо що всі символи - цифри
        if (!/^\d+$/.test(cardNumber)) {
            return false;
        }

        let sum = 0;
        let alternate = false;

        // Проходимо з кінця
        for (let i = cardNumber.length - 1; i >= 0; i--) {
            let digit = parseInt(cardNumber.charAt(i));

            if (alternate) {
                digit *= 2;
                if (digit > 9) {
                    digit = (digit % 10) + 1;
                }
            }

            sum += digit;
            alternate = !alternate;
        }

        return (sum % 10) === 0;
    }

    // Валідація терміну дії карти
    function validateCardExpiry() {
        const month = parseInt(cardMonth.value);
        const year = parseInt(cardYear.value);

        if (cardMonth.value.length === 2 && cardYear.value.length === 2) {
            const currentDate = new Date();
            const currentMonth = currentDate.getMonth() + 1;
            const currentYear = currentDate.getFullYear() % 100;

            const expiryDate = new Date(2000 + year, month - 1);
            const currentDateForComparison = new Date(currentDate.getFullYear(), currentDate.getMonth());

            if (expiryDate >= currentDateForComparison) {
                cardMonth.classList.remove('is-invalid');
                cardMonth.classList.add('is-valid');
                cardYear.classList.remove('is-invalid');
                cardYear.classList.add('is-valid');
            } else {
                cardMonth.classList.remove('is-valid');
                cardMonth.classList.add('is-invalid');
                cardYear.classList.remove('is-valid');
                cardYear.classList.add('is-invalid');
            }
        } else {
            cardMonth.classList.remove('is-valid', 'is-invalid');
            cardYear.classList.remove('is-valid', 'is-invalid');
        }
    }

    // Форматування IBAN
    ibanField.addEventListener('input', function () {
        let value = this.value.replace(/[^A-Z0-9]/gi, '').toUpperCase();

        if (value.length >= 2 && !value.startsWith('UA')) {
            value = 'UA' + value.substring(0, 32);
        }

        // Додаємо пробіли кожні 4 символи для зручності читання
        let formattedValue = value.replace(/(.{4})/g, '$1 ').trim();
        this.value = formattedValue;

        // Валідація IBAN
        validateIBAN(value.replace(/\s/g, ''));
    });

    // Валідація IBAN
    function validateIBAN(iban) {
        const ibanPattern = /^UA\d{2}[A-Z0-9]{26}$/;

        if (iban.length === 28 && ibanPattern.test(iban)) {
            ibanField.classList.remove('is-invalid');
            ibanField.classList.add('is-valid');
        } else if (iban.length >= 4) {
            ibanField.classList.remove('is-valid');
            ibanField.classList.add('is-invalid');
        } else {
            ibanField.classList.remove('is-valid', 'is-invalid');
        }
    }

    // Обробка відправки форми
    form.addEventListener('submit', function (e) {
        // Перевірка паролів
        if (password.value !== confirmPassword.value) {
            e.preventDefault();
            alert('Паролі не співпадають. Будь ласка, перевірте.');
            return;
        }

        // Формування платіжної інформації
        let paymentInfo = '';

        if (paymentType.value === 'card') {
            const cardNum = cardNumber.value.replace(/\s/g, '');
            const month = cardMonth.value.padStart(2, '0');
            const year = cardYear.value.padStart(2, '0');

            // Перевірка валідності карти (будь-які 16 цифр)
            if (cardNum.length !== 16 || !/^\d{16}$/.test(cardNum)) {
                e.preventDefault();
                alert('Введіть 16 цифр номера карти');
                return;
            }

            // Перевірка терміну дії
            if (month < 1 || month > 12 || year.length !== 2) {
                e.preventDefault();
                alert('Введіть коректний термін дії карти');
                return;
            }

            const currentDate = new Date();
            const expiryDate = new Date(2000 + parseInt(year), parseInt(month) - 1);
            if (expiryDate < new Date(currentDate.getFullYear(), currentDate.getMonth())) {
                e.preventDefault();
                alert('Термін дії карти не може бути в минулому');
                return;
            }

            paymentInfo = `Card: ${cardNumber.value} Valid: ${month}/${year}`;

        } else if (paymentType.value === 'iban') {
            const iban = ibanField.value.replace(/\s/g, '');
            if (iban.length !== 28 || !iban.startsWith('UA')) {
                e.preventDefault();
                alert('Введіть коректний IBAN рахунок');
                return;
            }
            paymentInfo = `IBAN: ${iban}`;

        } else if (paymentType.value === 'cash') {
            paymentInfo = 'Cash only';
        }

        // Записуємо в приховане поле
        hiddenPaymentInfo.value = paymentInfo;
    });

    // Автофокус на наступне поле при введенні
    cardMonth.addEventListener('input', function () {
        if (this.value.length === 2) {
            cardYear.focus();
        }
    });
});